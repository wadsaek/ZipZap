// NpgsqlDataReaderExt.cs - Part of the ZipZap project for storing files online
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

using System.Data;
using System.Threading;
using System.Threading.Tasks;

using Npgsql;

namespace ZipZap.Persistence.Extensions;

public static class NpgsqlDataReaderExt {
    extension(NpgsqlDataReader reader) {
        public T? GetNullableFieldValue<T>(int ordinal)
        where T : class =>
            reader.IsDBNull(ordinal) ? reader.GetFieldValue<T>(ordinal) : null;

        public async Task<T?> GetNullableFieldValueAsync<T>(int ordinal, CancellationToken token = default)
        where T : class =>
            await reader.IsDBNullAsync(ordinal, token) ? null : await reader.GetFieldValueAsync<T>(ordinal, token);

        public T? GetNullableFieldValue<T>(string name)
        where T : class =>
            reader.IsDBNull(name) ? reader.GetFieldValue<T>(name) : null;

        public async Task<T?> GetNullableFieldValueAsync<T>(string name, CancellationToken token = default)
        where T : class =>
            await reader.IsDBNullAsync(name, cancellationToken: token) ? null : await reader.GetFieldValueAsync<T>(name, token);
    }
}
