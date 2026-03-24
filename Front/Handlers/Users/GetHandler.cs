// GetHandler.cs - Part of the ZipZap project for storing files online
//     Copyright (C) 2026  Barenboim Esther wadsaek@gmail.com
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY, without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
//
//     You should have received a copy of the GNU General Public License
//     along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using ZipZap.Classes;
using ZipZap.Classes.Helpers;
using ZipZap.Front.Factories;
using ZipZap.Front.Services;
using ZipZap.LangExt.Helpers;

using static ZipZap.LangExt.Helpers.ResultConstructor;

namespace ZipZap.Front.Handlers.Users;

using static IGetHandler;

public interface IGetHandler {
    Task<Result<IResult, Error>> Get(HttpRequest request, UserId? id, CancellationToken cancellationToken);

    public interface IResult {
        User User { get; }
        List<UserSshKey> SshKeys { get; }

        List<FsoAccess> Shared { get; }
        List<FsoAccess> Accessible { get; }

        Func<CancellationToken, Task<Result<Unit, Error>>>? DeleteUser { get; }
        Func<SshPublicKey, CancellationToken, Task<Result<Unit, Error>>>? AddSshKey { get; }
    }

    public class Result : IResult {

        public Result(User user, List<UserSshKey> sshKeys, IBackend backend, bool isThisUser, List<FsoAccess> shared, List<FsoAccess> accessible) {
            User = user;
            SshKeys = sshKeys;
            _backend = backend;
            _isThisUser = isThisUser;
            Shared = shared;
            Accessible = accessible;
        }

        public User User { get; }
        public List<UserSshKey> SshKeys { get; }

        Func<CancellationToken, Task<Result<Unit, Error>>>? IResult.DeleteUser => _isThisUser ? DeleteUser : null;

        public List<FsoAccess> Shared { get; }

        public List<FsoAccess> Accessible { get; }

        Func<SshPublicKey, CancellationToken, Task<Result<Unit, Error>>>? IResult.AddSshKey => _isThisUser
            ? AddSshKey
            : null;

        private readonly IBackend _backend;
        private readonly bool _isThisUser;

        private Task<Result<Unit, Error>> DeleteUser(CancellationToken cancellationToken)
            => _backend.RemoveSelf(cancellationToken)
            .SelectAsync(_ => new Unit())
            .SelectErrAsync(err => err switch {
                ServiceError.NotFound => new Error.NotFound() as Error,
                ServiceError.Unauthorized => new Error.ShouldRedirect("/"),
                ServiceError.BadRequest => new Error.BadRequest(),
                _ => throw new NotImplementedException()
            });
        private Task<Result<Unit, Error>> AddSshKey(SshPublicKey key, CancellationToken cancellationToken)
            => _backend.AddSshKey(key, cancellationToken).SelectAsync(_ => new Unit())
            .SelectErrAsync(err => err switch {
                ServiceError.NotFound => new Error.NotFound() as Error,
                ServiceError.Unauthorized => new Error.ShouldRedirect("/"),
                ServiceError.BadRequest => new Error.BadRequest(),
                _ => throw new NotImplementedException()
            });
    };
    public abstract record Error {
        public sealed record NotFound : Error;

        public sealed record BadRequest : Error;

        public sealed record ShouldRedirect(string Target) : Error;

    }
}

public class GetHandler : IGetHandler {
    private readonly IBackendFactory _factory;
    private readonly ILogger<GetHandler> _logger;

    public GetHandler(IBackendFactory factory, ILogger<GetHandler> logger) {
        _factory = factory;
        _logger = logger;
    }

    public async Task<Result<IResult, Error>> Get(HttpRequest request, UserId? id, CancellationToken cancellationToken) {
        var token = request.Cookies[Constants.AUTHORIZATION];
        if (token is null) return Err<IResult, Error>(new Error.ShouldRedirect("/"));
        var backend = _factory.Create(new(token));
        var userResult = id switch {
            null => await backend.GetSelf(cancellationToken),
            _ => await backend.GetUserById(id.Value, cancellationToken)
        };

        return await userResult
            .SelectManyAsync(u => GetKeysForUser(id, backend, u, cancellationToken))
            .SelectManyAsync(res => GetSharedFsosForUser(id, backend, res, cancellationToken))
            .SelectErrAsync(err => err switch {
                ServiceError.NotFound => new Error.NotFound() as Error,
                ServiceError.Unauthorized => new Error.ShouldRedirect("/"),
                ServiceError.BadRequest => new Error.BadRequest(),
                _ => throw new NotImplementedException(err.ToString())
            });

    }

    private static async Task<Result<IResult, ServiceError>> GetSharedFsosForUser(UserId? id, IBackend backend, IntermidiateResult res, CancellationToken cancellationToken) {
        if (id is not UserId uId)
            return Ok<IResult, ServiceError>(new Result(
                res.User, res.Keys,
                backend,
                isThisUser: true,
                shared: [], accessible: [])
            );
        return await backend.GetAccessesForUserById(uId, cancellationToken)
            .SelectManyAsync(a => backend.GetAccessible().SelectAsync(async accesses => {
                List<FsoAccess> fsoAccesses = [];
                foreach (var acc in accesses) {
                    if (await backend.GetFsoOwner(acc.Fso.Id, cancellationToken).SelectAsync(u => u == res.User).UnwrapOrAsync(false))
                        fsoAccesses.Add(acc);
                }
                return fsoAccesses;
            }).SelectAsync(a2 => (shared: a, accessible: a2)))
        .SelectAsync(param => new Result(
            res.User,
            res.Keys,
            backend,
            isThisUser: false,
            param.shared.ToList(), param.accessible
        ) as IResult);
    }

    private static async Task<Result<IntermidiateResult, ServiceError>> GetKeysForUser(UserId? id, IBackend backend, User user, CancellationToken cancellationToken) {
        var keysResult = id is null
            ? await backend.GetSshKeys(cancellationToken)
            : Ok<IEnumerable<UserSshKeyRaw>, ServiceError>([]);

        return keysResult.Select(keys => new IntermidiateResult(
            user,
            keys
                .Select(k => k.Wrap(user))
                .ToList()
        ));
    }
    private record struct IntermidiateResult(User User, List<UserSshKey> Keys);
}
