// TrustedAuthorityKeysHelper.cs - Part of the ZipZap project for storing files online
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
using System.Data;
using System.Threading;
using System.Threading.Tasks;

using Npgsql;

using ZipZap.Classes;
using ZipZap.Persistence.Data;

namespace ZipZap.Persistence.Repositories;

using Key = TrustedAuthorityKey;

internal class TrustedAuthorityKeysHelper : EntityHelper<TrustedAuthorityKeyInner, Key, Guid> {
    public override string IdCol => GetColumnName(nameof(FsoAccessInner.Id));

    public override TrustedAuthorityKeyInner CloneWithId(TrustedAuthorityKeyInner entity, Guid id) {
        return new(entity) { Id = id };
    }

    public override async Task<Key> Parse(NpgsqlDataReader reader, CancellationToken token = default) {
        var inner = new TrustedAuthorityKeyInner(
             await reader.GetFieldValueAsync<Guid>($"{TableName}_{GetColumnName(nameof(TrustedAuthorityKeyInner.Id))}", token),
             await reader.GetFieldValueAsync<string>($"{TableName}_{GetColumnName(nameof(TrustedAuthorityKeyInner.ServerKey))}", token),
             await reader.GetFieldValueAsync<string>($"{TableName}_{GetColumnName(nameof(TrustedAuthorityKeyInner.ServerName))}", token),
             await reader.GetFieldValueAsync<Guid>($"{TableName}_{GetColumnName(nameof(TrustedAuthorityKeyInner.AdminId))}", token),
             await reader.GetFieldValueAsync<DateTimeOffset>($"{TableName}_{GetColumnName(nameof(TrustedAuthorityKeyInner.AddedTime))}", token)
        );
        return inner.Into();
    }
}
