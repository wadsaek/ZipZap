using System.Data;
using System.Threading;
using System.Threading.Tasks;

using Npgsql;

namespace ZipZap.FileService.Extensions;

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
