// SftpSubsystem.cs - Part of the ZipZap project for storing files online
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

using ZipZap.Sftp.Ssh;
using ZipZap.Sftp.Ssh.Services;
using ZipZap.Sftp.Ssh.Services.Connection;


namespace ZipZap.Sftp.Sftp;

internal class SftpSubsystem : SshBackgroundHandler<byte[], byte[]>, ISubsystem {
    private readonly ISftpRequestHandler _handler;
    private readonly IChannelClient _client;

    public SftpSubsystem(ISftpRequestHandler handler, ILogger<SftpSubsystem> logger, IChannelClient client) : base(logger) {
        _handler = handler;
        _client = client;
    }

    public override string ServiceName => throw new System.NotImplementedException();

    public Task SendData(byte[] payload, CancellationToken cancellationToken) {
        return Send(payload, cancellationToken);
    }

    protected override Task End(Disconnect disconnect, CancellationToken cancellationToken) {
        return _client.End(disconnect, cancellationToken);
    }

    protected override Task ReturnPacket(byte[] packet, CancellationToken cancellationToken) {
        return _client.ReturnPacket(packet, cancellationToken);
    }

    protected override Task HandlePacket(byte[] payload, CancellationToken cancellationToken) {
        throw new System.NotImplementedException();
    }

}

interface IChannelClient {
    Task End(Disconnect disconnect, CancellationToken cancellationToken);
    Task ReturnPacket(byte[] packet, CancellationToken cancellationToken);
}
class SftpFactory : ISftpFactory {
    private readonly ILogger<SftpSubsystem> _logger;

    public SftpFactory(ILogger<SftpSubsystem> logger) {
        _logger = logger;
    }

    ISubsystem? ISftpFactory.Create(ISftpRequestHandler handler, IChannelClient client) {
        return new SftpSubsystem(handler, _logger, client);
    }
}

interface ISftpFactory {
    ISubsystem? Create(ISftpRequestHandler handler, IChannelClient client);
}
