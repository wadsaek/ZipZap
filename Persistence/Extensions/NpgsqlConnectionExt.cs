// NpgsqlConnectionExt.cs - Part of the ZipZap project for storing files online
//     Copyright (C) 2026  Barenboim Esther wadsaek@gmail.com
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY

using System;
using System.Threading;
using System.Threading.Tasks;

using Npgsql;

using ZipZap.Classes.Helpers;

namespace ZipZap.Persistence.Extensions;

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
