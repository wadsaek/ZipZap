// FsoAccess.cs - Part of the ZipZap project for storing files online
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

using ZipZap.Classes.Helpers;

namespace ZipZap.Classes;

public sealed record FsoAccess(
    FsoAccessId Id,
    MaybeEntity<Fso, FsoId> Fso,
    MaybeEntity<User, UserId> User
) : IEntity<FsoAccessId>;

public record struct FsoAccessId(Guid Value) {
}
