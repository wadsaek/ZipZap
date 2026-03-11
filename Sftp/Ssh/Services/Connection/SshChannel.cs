// SshChannel.cs - Part of the ZipZap project for storing files online
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
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using ZipZap.Sftp.Ssh.Services.Connection.Packets;

namespace ZipZap.Sftp.Ssh.Services.Connection;

public record SshChannelData(uint PeerId, uint WindowSizeCtS, uint WindowSizeStC, uint PacketSizeCtS, uint PacketSizeStC, ClosedStatus ClosedStatus);

internal abstract class SshChannel : SshBackgroundHandler<ChannelData, byte[]>, ISshChannel {

    protected abstract Task HandleExtendedDataImpl(ChannelExtendedData data, CancellationToken cancellationToken);
    protected abstract Task HandlePacketImpl(byte[] data, CancellationToken cancellationToken);

    public SshChannel(SshChannelData channelData, ILogger logger, ITransportClient client) : base(logger) {
        _channelData = channelData;
        _logger = logger;
        _client = client;
    }
    private readonly ConcurrentQueue<IChannelPayload> PacketsToSend = [];

    private SshChannelData _channelData;

    public uint PeerId => _channelData.PeerId;
    public uint WindowSizeCtS => _channelData.WindowSizeCtS;
    public uint WindowSizeStC => _channelData.WindowSizeStC;
    public uint PacketSizeCtS => _channelData.PacketSizeCtS;
    public uint PacketSizeStC => _channelData.PacketSizeStC;
    public ClosedStatus Status => _channelData.ClosedStatus;
    public bool IsEof { get; set; }


    private readonly ILogger _logger;
    private readonly ITransportClient _client;

    public Task SendData(ChannelData payload, CancellationToken cancellationToken) {
        return Send(payload, cancellationToken);
    }

    public Task RegisterEof(CancellationToken cancellationToken) {
        IsEof = true;
        return Task.CompletedTask;
    }

    public Task Close(CancellationToken cancellationToken) {
        _channelData = _channelData with { ClosedStatus = ClosedStatus.Closed };
        return Task.CompletedTask;
    }

    public abstract Task HandleRequest(ChannelRequest request, CancellationToken cancellationToken);

    public Task AdjustWindow(uint bytesToAdd, CancellationToken cancellationToken) {
        _channelData = _channelData with { WindowSizeStC = _channelData.PacketSizeStC + bytesToAdd };
        return Task.CompletedTask;
    }
    protected override async Task End(Disconnect disconnect, CancellationToken cancellationToken) {
        await _client.SendPacket(disconnect, cancellationToken);
        _client.End();
    }

    protected override async Task ReturnPacket(byte[] bytes, CancellationToken cancellationToken) {
        var data = new ChannelData(PeerId, bytes);
        await SendOrEnqueue(data, cancellationToken);
    }

    private async Task SendOrEnqueue(IChannelPayload data, CancellationToken cancellationToken) {
        PacketsToSend.Enqueue(data);
        while (PacketsToSend.TryPeek(out var packet) && WindowSizeStC >= packet.ChannelWindowLength) {
            PacketsToSend.TryDequeue(out _);
            _channelData = _channelData with { WindowSizeStC = WindowSizeStC - packet.ChannelWindowLength };
            await _client.SendPacket(data, cancellationToken);
        }
    }

    public async Task HandleExtendedData(ChannelExtendedData data, CancellationToken cancellationToken) {
        await WindowRegisterReceivedPacket(data, cancellationToken);
        await HandleExtendedDataImpl(data, cancellationToken);
    }
    protected override async Task HandlePacket(ChannelData data, CancellationToken cancellationToken) {
        await WindowRegisterReceivedPacket(data, cancellationToken);
        await HandlePacketImpl(data.Data, cancellationToken);
    }

    private async ValueTask WindowRegisterReceivedPacket(IChannelPayload data, CancellationToken cancellationToken) {
        var newSize = WindowSizeCtS - data.ChannelWindowLength;
        if (newSize <= PacketSizeCtS) {
            newSize += PacketSizeCtS;
            var windowAdjust = new ChannelWindowAdjust(PeerId, PacketSizeCtS);
            await _client.SendPacket(windowAdjust, cancellationToken);
        }
        _channelData = _channelData with { WindowSizeCtS = newSize };
    }
}

internal interface IChannelPayload : IServerPayload {
    public uint ChannelWindowLength { get; }
}
