// TrustedAuthorityKeyInner.cs - Part of the ZipZap project for storing files online
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

[SqlTable("authorized_servers")]
public class TrustedAuthorityKeyInner : ITranslatable<TrustedAuthorityKey>, ISqlRetrievable, IInner<Guid> {
    public TrustedAuthorityKeyInner(Guid id, string key, string serverName, Guid adminId, DateTimeOffset timeAdded) {
        Id = id;
        ServerKey = key;
        ServerName = serverName;
        AdminId = adminId;
        AddedTime = timeAdded;
    }

    public TrustedAuthorityKeyInner(TrustedAuthorityKeyInner other) : this(
        other.Id,
        other.ServerName,
        other.ServerKey,
        other.AdminId,
        other.AddedTime
        ) { }


    public TrustedAuthorityKeyInner Copy() => new(this);

    [SqlColumn("id")]
    public Guid Id { get; init; }

    [SqlColumn("server_key")]
    public string ServerKey { get; init; }

    [SqlColumn("server_name")]
    public string ServerName { get; init; }

    [SqlColumn("admin_id")]
    public Guid AdminId { get; init; }

    [SqlColumn("added_time")]
    public DateTimeOffset AddedTime { get; init; }

    public static TrustedAuthorityKeyInner From(TrustedAuthorityKey key) => new(
        key.Id.Id,
        key.Key.Value,
        key.ServerName,
        key.Admin.Id.Value,
        key.TimeAdded
    );

    public TrustedAuthorityKey Into()
        => new(
            new(Id),
            ServerName,
            new(ServerKey),
            AddedTime,
            AdminId.ToUserId()
        );

    static ITranslatable<TrustedAuthorityKey> ITranslatable<TrustedAuthorityKey>.From(TrustedAuthorityKey entity)
        => From(entity);
}
