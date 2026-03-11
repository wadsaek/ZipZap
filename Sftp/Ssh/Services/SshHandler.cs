// SshHandler.cs - Part of the ZipZap project for storing files online
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
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using ZipZap.Sftp.Ssh.Numbers;

namespace ZipZap.Sftp.Ssh.Services;

internal abstract class SshBackgroundHandler<TReceive, TSend> {

    protected abstract Task ReturnPacket(TSend packet, CancellationToken cancellationToken);
    protected abstract Task End(Disconnect disconnect, CancellationToken cancellationToken);
    protected abstract Task HandlePacket(TReceive payload, CancellationToken cancellationToken);

    protected bool isDisposed = false;
    private readonly Lock _lock = new();
    private Task? _handler = null;

    private readonly ILogger _logger;

    private readonly ChannelReader<TReceive> _incomingReader;
    private readonly ChannelWriter<TReceive> _incomingPacketsWriter;

    protected SshBackgroundHandler(ILogger logger) {
        var channel = Channel.CreateUnbounded<TReceive>(new() {
            SingleReader = true,
            SingleWriter = true
        });
        _incomingReader = channel.Reader;
        _incomingPacketsWriter = channel.Writer;
        _logger = logger;
    }
    protected async Task<TReceive> ReadNextPacket(CancellationToken cancellationToken) => await _incomingReader.ReadAsync(cancellationToken);

    public abstract string ServiceName { get; }

    public async Task Send(TReceive packet, CancellationToken cancellationToken) {
        await _incomingPacketsWriter.WriteAsync(packet, cancellationToken);
        lock (_lock) {
            if (_handler == null || _handler.IsCompleted) {
                _handler = StartWorking(cancellationToken);
            }
        }
    }

    private async Task StartWorking(CancellationToken cancellationToken) {
        while (!isDisposed) {
            var packet = await ReadNextPacket(cancellationToken);
            var payload = packet;
            try {
                await HandlePacket(payload, cancellationToken);
            } catch(OperationCanceledException){
                return;
            }
            catch (Exception ex) {
                if (_logger.IsEnabled(LogLevel.Critical))
                    _logger.LogCritical("Received exception {Ex}", ex);

                var disconnectInternal = new Disconnect(
                    DisconnectCode.ServiceNotAvailable,
                    "internal server error"
                );

                await End(disconnectInternal, cancellationToken);
                return;
            }
        }
    }

    public void Dispose() {
        isDisposed = true;
    }
}
