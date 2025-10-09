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
            await reader.IsDBNullAsync(ordinal) ? null : await reader.GetFieldValueAsync<T>(ordinal,token);
    }
}
