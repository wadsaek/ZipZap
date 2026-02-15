// GuidExt.cs - Part of the ZipZap project for storing files online
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


namespace ZipZap.Classes.Extensions;

public static class GuidExt {
    extension(Guid guid) {
        public UserId ToUserId() => new(guid);
        public FsoId ToFsoId() => new(guid);
        public FsoAccessId ToFsoAccessId() => new(guid);
        public Grpc.Guid ToGrpcGuid() => new() { Value = guid.ToString() };
    }
}
