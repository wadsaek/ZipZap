// IPacketHandler.cs - Part of the ZipZap project for storing files online
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
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using ZipZap.Sftp.Ssh;
using ZipZap.Sftp.Ssh.Numbers;

namespace ZipZap.Sftp;

internal interface IPacketHandler {
    Task BeginHandle(Packet packet, CancellationToken cancellationToken);
}
internal interface IPacketHandlerFactory {
    public IPacketHandler Create(ITransportClient sendPacket);
}

internal class PacketHandlerFactory : IPacketHandlerFactory {

    private readonly ISftpRequestHandler _sftpRequestHandler;

    public PacketHandlerFactory(ISftpRequestHandler sftpRequestHandler) {
        _sftpRequestHandler = sftpRequestHandler;
    }
    public IPacketHandler Create(ITransportClient transport) => new PacketHandler(transport, _sftpRequestHandler);

}
public interface ITransportClient {
    void SendPacket<T>(T Packet) where T : IServerPayload;
}
internal class PacketHandler : IPacketHandler, IDisposable {

    private bool _isDisposed = false;
    private readonly Lock _lock = new();
    private Task? _handler = null;
    private readonly ITransportClient _transport;
    private readonly ISftpRequestHandler _sftpRequestHandler;
    private readonly ConcurrentQueue<Packet> _incomingPackets = [];

    public PacketHandler(ITransportClient sendPacket, ISftpRequestHandler sftpRequestHandler) {
        _transport = sendPacket;
        _sftpRequestHandler = sftpRequestHandler;
    }

    public Task BeginHandle(Packet packet, CancellationToken cancellationToken) {
        _incomingPackets.Enqueue(packet);
        lock (_lock) {
            if (_handler == null || _handler.IsCompleted) {
                if (_handler?.IsCompletedSuccessfully == false)
                    throw _handler.Exception!;
                _handler = StartWorking();
            }
        }

        return Task.CompletedTask;
    }

    private async Task StartWorking() {
        while (!_isDisposed && _incomingPackets.TryDequeue(out var packet)) {
            var payload = packet.Payload;
            if (payload.Length == 0) {
                _transport.SendPacket(new Disconnect(
                    DisconnectCode.ProtocolError,
                    "Zero length packet encountered"
                    ));
            }
            var stream = new MemoryStream(payload);
            if (!stream.SshTryReadByteSync(out var msg)) ;
        }
    }

    public void Dispose() {
        _isDisposed = true;
    }
}
