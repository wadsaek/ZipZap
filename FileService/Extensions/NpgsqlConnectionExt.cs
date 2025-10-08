using System;
using System.Threading.Tasks;
using Npgsql;

namespace ZipZap.FileService.Extensions;
public static class NpgsqlConnectionExt{
    extension(NpgsqlConnection connection){
        public async Task<IAsyncDisposable> OpenAsyncDisposable(){
            await connection.OpenAsync();
            return new DisposableOpenConnection(connection);
        }
        public NpgsqlCommand CreateCommand(string cmd){
            var command = connection.CreateCommand();
            command.CommandText = cmd;
            return command;
        }
    }
    private class DisposableOpenConnection : IAsyncDisposable, IDisposable {
        private readonly NpgsqlConnection _connection;

        public DisposableOpenConnection(NpgsqlConnection connection) {
            _connection = connection;
        }

        public void Dispose() =>
            _connection.Close();

        public async ValueTask DisposeAsync() {
            await _connection.CloseAsync();
        }
    }
}
