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

using ZipZap.Classes;
using ZipZap.Classes.Helpers;
using ZipZap.Front.Factories;
using ZipZap.Front.Services;
using ZipZap.LangExt.Helpers;
using ZipZap.Sftp.Ssh.Algorithms;

using static ZipZap.LangExt.Helpers.ResultConstructor;

namespace ZipZap.Front.Handlers.Settings;

using Error = IGetHandler.HandlerError;
using Result = IGetHandler.HandlerResult;

public interface IGetHandler {
    public Task<Result<Result, Error>> Handle(HttpRequest request, CancellationToken cancellationToken);
    public sealed record HandlerResult(List<TrustedAuthorityKeyWithUser> TrustedAuthorityKeys, SshPublicKey ThisServerKey);
    public abstract record HandlerError {
        public record ShouldRedirect(string Target) : Error;
    }
}

// TODO: finish
public class GetHandler : IGetHandler {
    private readonly IBackendFactory _factory;
    private readonly RsaServerKeyAlgorithm _keyAlgorithm;

    public GetHandler(IBackendFactory factory, RsaServerKeyAlgorithm keyAlgorithm) {
        _factory = factory;
        _keyAlgorithm = keyAlgorithm;
    }

    public async Task<Result<Result, Error>> Handle(HttpRequest request, CancellationToken cancellationToken) {
        var token = request.Cookies[Constants.AUTHORIZATION];
        if (token is null) return Err<Result, Error>(new Error.ShouldRedirect("/"));
        var backend = _factory.Create(new(token));
        return await backend.GetSshHostKeys(cancellationToken)
        .SelectAsync(keys => new Result(
            keys.ToList(),
            new(
                _keyAlgorithm.GetHostKeyPair()
                    .GetPublicKey()
                    .ToAsciiString())))
        .SelectErrAsync(err => err switch {
            ServiceError.Unauthorized => new Error.ShouldRedirect("/") as Error,
            ServiceError.Unknown or _ => throw new NotImplementedException(err.ToString())
        });

    }
}
