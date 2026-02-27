// SshService.cs - Part of the ZipZap project for storing files online
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

using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using ZipZap.Sftp.Ssh.Numbers;

namespace ZipZap.Sftp.Ssh.Services;

internal abstract class SshService : ISshService {

    protected abstract Task ReturnPacket<T>(T packet, CancellationToken cancellationToken) where T : IServerPayload;
    protected abstract Task End(CancellationToken cancellationToken);
    protected abstract Task HandlePacket(Packet packet, CancellationToken cancellationToken);

    protected bool isDisposed = false;
    private readonly Lock _lock = new();
    private Task? _handler = null;

    private readonly ChannelReader<Packet> _incomingReader;
    private readonly ChannelWriter<Packet> _incomingPacketsWriter;

    protected SshService() {
        var channel = Channel.CreateUnbounded<Packet>(new() {
            SingleReader = true,
            SingleWriter = true
        });
        _incomingReader = channel.Reader;
        _incomingPacketsWriter = channel.Writer;
    }
    protected async Task<Packet> ReadNextPacket() => await _incomingReader.ReadAsync();

    public abstract string ServiceName { get; }

    public async Task SendPacket(Packet packet, CancellationToken cancellationToken) {
        await _incomingPacketsWriter.WriteAsync(packet, cancellationToken);
        lock (_lock) {
            if (_handler == null || _handler.IsCompleted) {
                if (_handler?.IsCompletedSuccessfully == false)
                    throw _handler.Exception!;
                _handler = StartWorking(cancellationToken);
            }
        }
    }

    private async Task StartWorking(CancellationToken cancellationToken) {
        while (!isDisposed) {
            var packet = await _incomingReader.ReadAsync(cancellationToken);
            var payload = packet.Payload;
            if (payload.Length == 0) {
                await ReturnPacket(new Disconnect(
                    DisconnectCode.ProtocolError,
                    "Zero length packet encountered"
                    ), cancellationToken);
                await End(cancellationToken);
                return;
            }
            await HandlePacket(packet, cancellationToken);
        }
    }

    public void Dispose() {
        isDisposed = true;
    }
}
