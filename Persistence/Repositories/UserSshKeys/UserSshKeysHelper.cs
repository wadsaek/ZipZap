// UserSshKeysHelper.cs - Part of the ZipZap project for storing files online
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

internal class UserSshKeysHelper : EntityHelper<UserSshKeyInner, UserSshKey, Guid> {
    public override string IdCol => GetColumnName(nameof(FsoAccessInner.Id));

    public override UserSshKeyInner CloneWithId(UserSshKeyInner entity, Guid id) {
        return new(entity) { Id = id };
    }

    public override async Task<UserSshKey> Parse(NpgsqlDataReader reader, CancellationToken token = default) {
        var inner = new UserSshKeyInner(
             await reader.GetFieldValueAsync<Guid>($"{TableName}_{GetColumnName(nameof(UserSshKeyInner.Id))}", token),
             await reader.GetFieldValueAsync<string>($"{TableName}_{GetColumnName(nameof(UserSshKeyInner.Key))}", token),
             await reader.GetFieldValueAsync<Guid>($"{TableName}_{GetColumnName(nameof(UserSshKeyInner.UserId))}", token)
        );
        return inner.Into();
    }
}
