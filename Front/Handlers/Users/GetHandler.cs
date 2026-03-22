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

        Task<Result<Unit, Error>> DeleteUser(CancellationToken cancellationToken);
    }

    public class Result : IResult {

        public Result(User user, List<UserSshKey> sshKeys, IBackend backend) {
            User = user;
            SshKeys = sshKeys;
            _backend = backend;
        }

        public User User { get; }
        public List<UserSshKey> SshKeys { get; }
        private readonly IBackend _backend;
        public Task<Result<Unit, Error>> DeleteUser(CancellationToken cancellationToken)
            => _backend.AdminRemoveUser(User.Id, cancellationToken)
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
        var userResult =
            id is not null
            ? await backend.GetUserById(id.Value, cancellationToken)
            : await backend.GetSelf(cancellationToken);

        return await userResult
            .SelectManyAsync(u => GetKeysForUser(id, backend, u, cancellationToken))
            .SelectErrAsync(err => err switch {
                ServiceError.NotFound => new Error.NotFound() as Error,
                ServiceError.Unauthorized => new Error.ShouldRedirect("/"),
                ServiceError.BadRequest => new Error.BadRequest(),
                _ => throw new NotImplementedException(err.ToString())
            });

    }

    private static async Task<Result<IResult, ServiceError>> GetKeysForUser(UserId? id, IBackend backend, User user, CancellationToken cancellationToken) {
        var keysResult =
            id is not null
            ? await backend.AdminGetSshKeysForUser(id.Value, cancellationToken)
            : await backend.GetSshKeys(cancellationToken);

        return keysResult.Select(keys => new Result(
            user,
            keys
                .Select(k => k.Wrap(user))
                .ToList(),
           backend
        ) as IResult);
    }
}
