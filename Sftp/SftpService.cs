// SftpService.cs - Part of the ZipZap project for storing files online
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
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace ZipZap.Sftp;

internal class SftpService {

    private readonly ISftpRequestHandler _handler;
    private readonly ISftpConfiguration _configuration;
    private readonly ILogger<SftpService> _logger;
    private readonly Transport _transport;

    public SftpService(Transport transport, ILogger<SftpService> logger, ISftpConfiguration configuration, ISftpRequestHandler handler) {
        _transport = transport;
        _logger = logger;
        _configuration = configuration;
        _handler = handler;
    }

    internal async Task HandleSocket(Socket socket, CancellationToken cancellationToken) {
        try {
            using var _ = socket;
            await using var stream = new NetworkStream(socket);

            var idStrings = await ExchangeIdentificationStrings(stream, cancellationToken);

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("handling client {ClientId}", idStrings.Client);

            await _transport.Handle(stream, idStrings, cancellationToken);

        } catch (OperationCanceledException e) {
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("Operation was cancelled: {e}", e.Message);
        } catch (IOException e) {
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("IOException encountered: {e}", e.Message);
        }
    }

    private async Task<IdenitificationStrings> ExchangeIdentificationStrings(Stream stream, CancellationToken cancellationToken) {
        var serverHeader = $"SSH-2.0-{_configuration.ServerName}_{_configuration.Version}";
        var header = Encoding.ASCII.GetBytes(serverHeader);
        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(Encoding.ASCII.GetBytes("\r\n"), cancellationToken);

        var clientHeaderBytes = new List<byte>();
        while (clientHeaderBytes.Count < 2 || (clientHeaderBytes[^2] != (byte)'\r' && clientHeaderBytes[^1] != (byte)'\n'))
            clientHeaderBytes.Add((byte)stream.ReadByte());
        clientHeaderBytes.RemoveAt(clientHeaderBytes.Count - 1);
        clientHeaderBytes.RemoveAt(clientHeaderBytes.Count - 1);
        var clientHeader = Encoding.ASCII.GetString(clientHeaderBytes.ToArray());
        return new(serverHeader, clientHeader);
    }


}

