using System;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using ZipZap.FileService.Helpers;

namespace ZipZap.FileService.Extensions;

public static class NpgsqlConnectionExt {
    extension(NpgsqlConnection connection) {
        public async Task<IAsyncDisposable> OpenAsyncDisposable(CancellationToken token = default) {
            await connection.OpenAsync(token);
            return new Deffered<NpgsqlConnection>(connection,
                    conn => conn.Close(),
                    conn => conn.CloseAsync()
                    );
        }

        public NpgsqlCommand CreateCommand(string cmd) {
            var command = connection.CreateCommand();
            command.CommandText = cmd;
            return command;
        }
    }
}
