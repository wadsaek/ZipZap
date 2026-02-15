// ServiceError.cs - Part of the ZipZap project for storing files online
//     Copyright (C) 2026  Barenboim Esther wadsaek@gmail.com
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY

using System;

namespace ZipZap.Front.Services;

public abstract record ServiceError {
    public sealed record FailedPrecondition(string Detail) : ServiceError;
    public sealed record Unauthorized : ServiceError;
    public sealed record NotFound : ServiceError;
    public sealed record BadResult : ServiceError;
    public sealed record BadRequest : ServiceError;
    public sealed record AlreadyExists : ServiceError;
    public sealed record Unknown(Exception Exception) : ServiceError;
}
