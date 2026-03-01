// SshConnection.cs - Part of the ZipZap project for storing files online
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
using System.Threading;
using System.Threading.Tasks;

using ZipZap.Sftp.Ssh;
using ZipZap.Sftp.Ssh.Connection;
using ZipZap.Sftp.Ssh.Numbers;
using ZipZap.Sftp.Ssh.Services;

namespace ZipZap.Sftp;

internal interface ISshConnection : ISshService {
    public new const string ServiceName = "ssh-connection";
}
internal interface ISshConnectionFactory {
    public const string ServiceName = ISshConnection.ServiceName;
    public ISshConnection Create(ITransportClient sendPacket, ISftpRequestHandler handler);
}

internal class PacketHandlerFactory : ISshConnectionFactory {

    public ISshConnection Create(ITransportClient transport, ISftpRequestHandler handler)
        => new SshConnection(transport, handler);

}
internal class SshConnection : SshService, ISshConnection, IDisposable {

    private readonly ITransportClient _transport;
    private readonly ISftpRequestHandler _requestHandler;

    private readonly Dictionary<int, SshChannel> dictionary = [];

    public SshConnection(ITransportClient sendPacket, ISftpRequestHandler sftpRequestHandler) {
        _transport = sendPacket;
        _requestHandler = sftpRequestHandler;
    }

    override public string ServiceName => ISshConnection.ServiceName;

    protected override Task End(CancellationToken cancellationToken) {
        _transport.End();
        return Task.CompletedTask;
    }

    protected override async Task HandlePacket(Payload packet, CancellationToken cancellationToken) {
        var msg = (Message)packet[0];
        switch (msg) {
            case Message.GlobalRequest: {
                // we don't support tcp forwarding which is the only global request
                await _transport.SendPacket(new RequestFailure(),cancellationToken);
                return;
            }

        }
        var disconnect = new Disconnect(DisconnectCode.ServiceNotAvailable, "ssh-connection service not implemented");
        await _transport.SendPacket(disconnect, cancellationToken);
        await End(cancellationToken);
    }

    protected override Task ReturnPacket<T>(T packet, CancellationToken cancellationToken) {
        return _transport.SendPacket(packet, cancellationToken);
    }
}

internal interface ISshService {
    public string ServiceName { get; }
    public Task SendPacket(Payload packet, CancellationToken cancellationToken);
}

internal class SshChannel {
}
