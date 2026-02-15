// ExceptionHandler.cs - Part of the ZipZap project for storing files online
//     Copyright (C) 2026  Barenboim Esther wadsaek@gmail.com
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY

using Grpc.Core;

using ZipZap.Classes.Helpers;
using ZipZap.Front.Services;

namespace ZipZap.Front.Handlers.Exceptions;

public static class ServiceExceptionHandler {
    public static ExceptionConverter<ServiceError> GetExceptionConverter() => new SimpleExceptionConverter<ServiceError>(ex => ex switch {
        RpcException { StatusCode: StatusCode.Unauthenticated or StatusCode.PermissionDenied } => new ServiceError.Unauthorized(),
        RpcException { StatusCode: StatusCode.NotFound } => new ServiceError.NotFound(),
        RpcException { StatusCode: StatusCode.FailedPrecondition, Status.Detail: var detail } => new ServiceError.FailedPrecondition(detail),
        RpcException { StatusCode: StatusCode.AlreadyExists } => new ServiceError.AlreadyExists(),
        _ => new ServiceError.Unknown(ex)

    });
}
