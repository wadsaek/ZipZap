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

using Microsoft.Extensions.Logging;

using ZipZap.Sftp.Sftp;
using ZipZap.Sftp.Ssh.Numbers;
using ZipZap.Sftp.Ssh.Services.Connection.Packets;

namespace ZipZap.Sftp.Ssh.Services.Connection;

using SessionOpen = ChannelOpen.ChannelOpenGen<ChannelSpecificData.Session>;
using SessionConfirm = ChannelOpenConfirmation<ChannelSpecificData.Session>;

internal interface ISshConnection : ISshService {
    public new const string ServiceName = "ssh-connection";
}
internal interface ISshConnectionFactory {
    public const string ServiceName = ISshConnection.ServiceName;
    public ISshConnection Create(ITransportClient sendPacket, ISftpRequestHandler handler);
}

internal class SshConnectionFactory : ISshConnectionFactory {

    private readonly ILogger<SshConnection> _logger;
    private readonly ISshChannelFactory _channelFactory;

    public SshConnectionFactory(ILogger<SshConnection> logger, ISshChannelFactory channelFactory) {
        _logger = logger;
        _channelFactory = channelFactory;
    }

    public ISshConnection Create(ITransportClient transport, ISftpRequestHandler handler)
        => new SshConnection(transport, handler, _logger, _channelFactory);

}
internal class SshConnection : SshService, ISshConnection, IDisposable {

    private readonly ITransportClient _transport;
    private readonly ISftpRequestHandler _requestHandler;
    private readonly ILogger<SshConnection> _logger;
    private readonly List<ISshChannel> _channels = [];
    private readonly ISshChannelFactory _channelFactory;

    public SshConnection(ITransportClient sendPacket, ISftpRequestHandler sftpRequestHandler, ILogger<SshConnection> logger, ISshChannelFactory channelFactory) : base(logger) {
        _transport = sendPacket;
        _requestHandler = sftpRequestHandler;
        _logger = logger;
        _channelFactory = channelFactory;
    }

    override public string ServiceName => ISshConnection.ServiceName;

    protected override async Task End(Disconnect disconnect, CancellationToken cancellationToken) {
        await ReturnPacket(disconnect, cancellationToken);
        _transport.End();
    }

    protected override async Task HandlePacket(Payload packet, CancellationToken cancellationToken) {
        var msg = (Message)packet[0];
        switch (msg) {
            case Message.GlobalRequest: {
                    if (!GlobalRequest.TryParse(packet, out var request)) {
                        await Unparsable(nameof(GlobalRequest), cancellationToken);
                        return;

                    }
                    if (request.WantReply)
                        // we don't support tcp forwarding which is the only defined global request
                        await _transport.SendPacket(new RequestFailure(), cancellationToken);
                    return;
                }
            case Message.ChannelOpen: {
                    if (!ChannelOpen.TryParse(packet, out var request)) {
                        await Unparsable(nameof(ChannelOpen), cancellationToken);
                        return;
                    }
                    if (request is not SessionOpen sessionRequest) {
                        var failurePacket = new ChannelOpenFailure(
                            request.SenderChannel,
                            ChannelOpenFailureCode.UnknownChannelType,
                            "Only the \"session\" channel is supported");
                        await _transport.SendPacket(failurePacket, cancellationToken);
                        return;
                    }
                    var channelId = await OpenSession(sessionRequest, cancellationToken);
                    var channel = _channels[(int)channelId];
                    var confirmation = new SessionConfirm(
                        channel.PeerId,
                        channelId,
                        channel.WindowSizeCtS,
                        channel.PacketSizeCtS,
                        new()
                    );
                    await ReturnPacket(confirmation, cancellationToken);
                    return;
                }
            case Message.ChannelRequest: {
                    if (!ChannelRequest.TryParse(packet, out var request)) {
                        await Unparsable(nameof(ChannelRequest), cancellationToken);
                        return;
                    }
                    if (_logger.IsEnabled(LogLevel.Information))
                        _logger.LogInformation("Received request {Request}", request);
                    if (await TryGetChannel(request.Recipient, cancellationToken) is not ISshChannel channel) return;
                    await channel.HandleRequest(request, cancellationToken);
                    break;
                }
            case Message.ChannelData: {
                    if (!ChannelData.TryParse(packet, out var data)) {
                        await Unparsable(nameof(ChannelData), cancellationToken);
                        return;
                    }
                    if (await TryGetChannel(data.RecipientChannel, cancellationToken) is not ISshChannel channel) return;
                    await channel.SendData(data, cancellationToken);
                    break;
                }
            default: {
                    var disconnect_ = new Disconnect(
                            DisconnectCode.ServiceNotAvailable,
                            "ssh-connection service not implemented"
                    );
                    await End(disconnect_, cancellationToken);
                    return;
                }
        }
    }

    private async ValueTask<ISshChannel?> TryGetChannel(uint Recipient, CancellationToken cancellationToken) {
        if (!await IsValidRecipient(Recipient, cancellationToken)) return null;
        return _channels[(int)Recipient];
    }
    private async ValueTask<bool> IsValidRecipient(uint Recipent, CancellationToken cancellationToken) {
        if (Recipent >= _channels.Count || _channels[(int)Recipent].IsClosed) {
            await BadPacket("Invalid channel requested.", cancellationToken);
            return false;
        }
        return true;
    }

    private Task<uint> OpenSession(SessionOpen sessionRequest, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();

        var channel = _channelFactory.CreateSessionChannel(
            sessionRequest.SenderChannel,
            sessionRequest.WindowSize,
            sessionRequest.MaxPacketSize,
            _transport, _requestHandler
        );
        _channels.Add(channel);
        return Task.FromResult((uint)_channels.Count - 1);
    }

    private async Task BadPacket(string message, CancellationToken cancellationToken) {

        var disconnect = new Disconnect(DisconnectCode.ProtocolError, message);
        await End(disconnect, cancellationToken);
    }
    private Task Unparsable(string packetName, CancellationToken cancellationToken) => BadPacket($"Unable to parse {packetName}", cancellationToken);

    protected override Task ReturnPacket(IServerPayload packet, CancellationToken cancellationToken) {
        return _transport.SendPacket(packet, cancellationToken);
    }

}

internal interface ISshService {
    public string ServiceName { get; }
    public Task Send(Payload packet, CancellationToken cancellationToken);
}
internal interface ISshChannelFactory {
    public ISshChannel CreateSessionChannel(uint peerId, uint windowSizeStC, uint packetSizeStC, ITransportClient client, ISftpRequestHandler requestHandler);
}
internal class SshChannelFactory : ISshChannelFactory {
    private readonly ILogger<SshChannel> _logger;
    private readonly ISftpFactory _sftpFactory;

    public SshChannelFactory(ILogger<SshChannel> logger, ISftpFactory sftpFactory) {
        _logger = logger;
        _sftpFactory = sftpFactory;
    }

    public ISshChannel CreateSessionChannel(uint peerId, uint windowSizeStC, uint packetSizeStC, ITransportClient client, ISftpRequestHandler requestHandler) {
        SshChannelData channelData = new(
            peerId,
            128 * 1024,
            windowSizeStC,
            128 * 1024,
            packetSizeStC,
            false);
        return new SshSessionChannel(channelData, _logger, client, _sftpFactory, requestHandler);
    }
}
