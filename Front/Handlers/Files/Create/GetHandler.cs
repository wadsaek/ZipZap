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

using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

using ZipZap.Classes;
using ZipZap.Classes.Helpers;
using ZipZap.Front.Factories;
using ZipZap.Front.Services;
using ZipZap.LangExt.Helpers;

using static ZipZap.LangExt.Helpers.ResultConstructor;

namespace ZipZap.Front.Handlers.Files.Create;

public class GetHandler {
    public User User { get; }
    public Fso Fso { get; }

    public GetHandler(Fso fso, User user) {
        Fso = fso;
        User = user;
    }

    public static Task<Result<GetHandler, GetHandlerError>> OnGetAsync(FsoId id, HttpRequest request, IBackendFactory backendFactory, System.Threading.CancellationToken cancellationToken) {
        var token = request.Cookies[Constants.AUTHORIZATION];
        if (token is null)
            return Task.FromResult(Err<GetHandler, GetHandlerError>(new GetHandlerError.Unauthorized()));
        var backend = backendFactory.Create(new(token));
        return backend.GetFsoByIdAsync(id, cancellationToken)
        .WithUser(backend, cancellationToken)
        .SelectAsync(param => new GetHandler(param.Item1, param.Item2))
        .SelectErrAsync(err => err switch {
            ServiceError.NotFound => new GetHandlerError.NotFound() as GetHandlerError,
            ServiceError.Unauthorized => new GetHandlerError.Unauthorized(),
            _ => new GetHandlerError.HandlerServiceError(err)
        });
    }

    public abstract record GetHandlerError {
        public sealed record NotFound : GetHandlerError;

        public sealed record Unauthorized : GetHandlerError;
        public sealed record HandlerServiceError(ServiceError Error) : GetHandlerError;
    }

}
