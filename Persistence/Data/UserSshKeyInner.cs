// UserSshKeyInner.cs - Part of the ZipZap project for storing files online
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

using ZipZap.Classes;
using ZipZap.Classes.Extensions;
using ZipZap.Persistence.Attributes;

namespace ZipZap.Persistence.Data;

[SqlTable("ssh_keys")]
public class UserSshKeyInner : ITranslatable<UserSshKey>, ISqlRetrievable, IInner<Guid> {

    public UserSshKeyInner(UserSshKeyInner other) : this(
        other.Id,
        other.Key,
        other.UserId
        ) { }

    public UserSshKeyInner(Guid id, string key, Guid userId) {
        Id = id;
        Key = key;
        UserId = userId;
    }

    public UserSshKeyInner Copy() => new(this);

    [SqlColumn("id")]
    public Guid Id { get; init; }

    [SqlColumn("key")]
    public string Key { get; init; }

    [SqlColumn("user_id")]
    public Guid UserId { get; init; }

    public static UserSshKeyInner From(UserSshKey key) => new(
        key.Id.Id,
        key.Key.Value,
        key.User.Id.Value
    );

    public UserSshKey Into()
        => new(
            new(Id),
            new(Key),
            UserId.ToUserId()
        );

    static ITranslatable<UserSshKey> ITranslatable<UserSshKey>.From(UserSshKey entity)
        => From(entity);
}
