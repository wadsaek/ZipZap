// SshSessionChannel.cs - Part of the ZipZap project for storing files online
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
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using ZipZap.Sftp.Sftp;
using ZipZap.Sftp.Ssh.Numbers;
using ZipZap.Sftp.Ssh.Services.Connection.Packets;

namespace ZipZap.Sftp.Ssh.Services.Connection;

using SubsystemRequest = ChannelRequest.ChannelRequestGen<ChannelRequestSpecificData.Subsystem>;

internal class SshSessionChannel : SshChannel {
    private readonly ITransportClient _client;
    private readonly ISftpFactory _factory;
    private readonly ISftpRequestHandler _handler;

    public SshSessionChannel(
        SshChannelData channelData,
        ILogger logger,
        ITransportClient client,
        ISftpFactory sftpFactory,
        ISftpRequestHandler handler
    )
        : base(channelData, logger, client) {
        _client = client;
        _factory = sftpFactory;
        _handler = handler;
    }

    private ISubsystem? _subsystem = null;

    public override string ServiceName => "session";

    protected override async Task HandleExtendedDataImpl(ChannelExtendedData data, CancellationToken cancellationToken) {
        if (!IsEof) return;
        var disconnect = new Disconnect(DisconnectCode.ProtocolError, "Extended data sent after having sent EOF");
        await End(disconnect, cancellationToken);

    }
    public override async Task HandleRequest(ChannelRequest request, CancellationToken cancellationToken) {
        switch (request) {
            case SubsystemRequest req: {
                    await TryOpenSubsystem(req,cancellationToken);
                    return;
                }
        }
    }

    private async Task TryOpenSubsystem(SubsystemRequest req,CancellationToken cancellationToken) {
        var success = false;
        if (req.SpecificDataGen.Name == "sftp") {
            _subsystem = _factory.Create(_handler, new SessionClient(this));
            success = true;
        }
        if (req.WantReply) {
            IServerPayload packet = success ? new ChannelSuccess(PeerId) : new ChannelFailure(PeerId);
            await _client.SendPacket(packet,cancellationToken);
        }
    }

    protected override async Task HandlePacketImpl(byte[] payload, CancellationToken cancellationToken) {
        if (_subsystem is not null)
            await _subsystem.SendData(payload, cancellationToken);
    }

    private class SessionClient : IChannelClient {
        private readonly SshSessionChannel sshSessionChannel;

        public SessionClient(SshSessionChannel sshSessionChannel) {
            this.sshSessionChannel = sshSessionChannel;
        }

        public Task End(Disconnect disconnect, CancellationToken cancellationToken) {
            return sshSessionChannel.End(disconnect, cancellationToken);
        }

        public Task ReturnPacket(byte[] packet, CancellationToken cancellationToken) {
            return sshSessionChannel.ReturnPacket(packet, cancellationToken);
        }
    }
}


internal interface ISubsystem {
    Task SendData(byte[] payload, CancellationToken cancellationToken);
}
