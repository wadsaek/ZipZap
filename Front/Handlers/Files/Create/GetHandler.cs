// GetHandler.cs - Part of the ZipZap project for storing files online
//     Copyright (C) 2026  Barenboim Esther wadsaek@gmail.com
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY

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
    public static async Task<Result<GetHandler, GetHandlerError>> OnGetAsync(FsoId id, HttpRequest request, IFactory<IBackend, BackendConfiguration> backendFactory) {
        var token = request.Cookies[Constants.AUTHORIZATION];
        if (token is null)
            return Err<GetHandler, GetHandlerError>(new GetHandlerError.Unauthorized());
        var backend = backendFactory.Create(new(token));
        var fsoResult = await backend.GetFsoByIdAsync(id);
        if (fsoResult is Err<Fso, ServiceError>(var err)) {
            return err switch {
                ServiceError.NotFound => Err<GetHandler, GetHandlerError>(new GetHandlerError.NotFound()),
                ServiceError.Unauthorized => Err<GetHandler, GetHandlerError>(new GetHandlerError.Unauthorized()),
                _ => Err<GetHandler, GetHandlerError>(new GetHandlerError.HandlerServiceError(err))
            };
        }

        return Ok<GetHandler, GetHandlerError>(new());
    }

    public abstract record GetHandlerError {
        public sealed record NotFound : GetHandlerError;

        public sealed record Unauthorized : GetHandlerError;
        public sealed record HandlerServiceError(ServiceError Error) : GetHandlerError;
    }

}
