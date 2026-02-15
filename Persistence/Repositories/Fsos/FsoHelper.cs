// FsoHelper.cs - Part of the ZipZap project for storing files online
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
using ZipZap.Persistence.Extensions;

namespace ZipZap.Persistence.Repositories;

internal class FsoHelper : EntityHelper<FsoInner, Fso, Guid> {
    public override string IdCol => GetColumnName(nameof(FsoInner.Id));

    public override FsoInner CloneWithId(FsoInner entity, Guid id) {
        return new(entity) { Id = id };
    }

    public override async Task<Fso> Parse(NpgsqlDataReader reader, CancellationToken token = default) {
        var inner = new FsoInner(
             await reader.GetFieldValueAsync<Guid>($"{TableName}_{GetColumnName(nameof(FsoInner.Id))}", token),
             await reader.GetFieldValueAsync<string>($"{TableName}_{GetColumnName(nameof(FsoInner.FsoName))}", token),
             await reader.GetFieldValueAsync<Guid?>($"{TableName}_{GetColumnName(nameof(FsoInner.VirtualLocationId))}", token),
             await reader.GetFieldValueAsync<short>($"{TableName}_{GetColumnName(nameof(FsoInner.Permissions))}", token),
             await reader.GetFieldValueAsync<int>($"{TableName}_{GetColumnName(nameof(FsoInner.FsoOwner))}", token),
             await reader.GetFieldValueAsync<int>($"{TableName}_{GetColumnName(nameof(FsoInner.FsoGroup))}", token),
             await reader.GetFieldValueAsync<FsoType>($"{TableName}_{GetColumnName(nameof(FsoInner.FsoType))}", token),
             await reader.GetNullableFieldValueAsync<string>($"{TableName}_{GetColumnName(nameof(FsoInner.LinkRef))}", token)
        );
        return inner.Into();
    }
}
