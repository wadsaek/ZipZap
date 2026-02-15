// FsoStatus.cs - Part of the ZipZap project for storing files online
//     Copyright (C) 2026  Barenboim Esther wadsaek@gmail.com
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY

using System.ComponentModel;

using ZipZap.Classes;
using ZipZap.LangExt.Helpers;

namespace ZipZap.Front.Services;

public abstract record FsoStatus {
    public sealed record Success(Fso Fso) : FsoStatus;
    public sealed record ParseError : FsoStatus;
    public sealed record StatusServiceError(ServiceError Error) : FsoStatus;

    public static FsoStatus FromServiceResult(Result<Fso, ServiceError> result)
        => result switch {
            Err<Fso, ServiceError>(var err) => new StatusServiceError(err),
            Ok<Fso, ServiceError>(var fso) => new Success(fso),
            _ => throw new InvalidEnumArgumentException()

        };
}

