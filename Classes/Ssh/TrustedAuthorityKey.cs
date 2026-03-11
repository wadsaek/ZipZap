// TrustedAuthorityKey.cs - Part of the ZipZap project for storing files online
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
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using ZipZap.Classes.Helpers;

namespace ZipZap.Classes;

public sealed record TrustedAuthorityKey(
    TrustedAuthorityKeyId Id,
    string ServerName,
    SshPublicKey Key,
    DateTimeOffset TimeAdded,
    MaybeEntity<User, UserId>? Admin
) : IEntity<TrustedAuthorityKeyId> {

    public TrustedAuthorityKeyWithUser WithUser(User? user)
        => new(
            Id, ServerName, Key, TimeAdded,
            Admin is not null ? user : null
        );
    [OverloadResolutionPriority(1)]
    public TrustedAuthorityKeyWithUser WithUserOr(Func<UserId, User> user)
        => new(
            Id, ServerName, Key, TimeAdded,
            Admin is not null ?
                (Admin as ExistsEntity<User, UserId>)?.Entity ?? user(Admin.Id)
                : null
        );
    public async Task<TrustedAuthorityKeyWithUser> WithUserOr(Func<UserId, Task<User>> user)
        => new(
            Id, ServerName, Key, TimeAdded,
            Admin is not null
                ? (Admin as ExistsEntity<User, UserId>)?.Entity ?? await user(Admin.Id)
                : null
        );
};

public sealed record TrustedAuthorityKeyWithUser(
    TrustedAuthorityKeyId Id,
    string ServerName,
    SshPublicKey Key,
    DateTimeOffset TimeAdded,
    User? Admin
) : IEntity<TrustedAuthorityKeyId> {
    public TrustedAuthorityKey ToRegularKey()
        => new(Id, ServerName, Key, TimeAdded, Admin is not null ? new ExistsEntity<User, UserId>(Admin) : null);
}

public record struct TrustedAuthorityKeyId(Guid Id) : IStrongId;
