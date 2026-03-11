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

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using ZipZap.LangExt.Helpers;
using ZipZap.Sftp.Sftp.Numbers;

using ZipZap.Sftp.Ssh;
using ZipZap.Sftp.Ssh.Services.Connection;

namespace ZipZap.Sftp.Sftp;

internal class SftpSubsystem : ISubsystem {
    private readonly ISftpRequestHandler _handler;
    private readonly IChannelClient _client;
    private readonly ILogger<SftpSubsystem> _logger;
    private readonly SftpPacketReader _reader = new();

    public SftpSubsystem(ISftpRequestHandler handler, ILogger<SftpSubsystem> logger, IChannelClient client) {
        _handler = handler;
        _client = client;
        _logger = logger;
    }

    private bool _isInitialized = false;
    public static string SubsystemName => "sftp";
    private Task? _process = null;

    public async Task SendData(byte[] payload, CancellationToken cancellationToken) {
        await _reader.RegisterData(payload, cancellationToken);
        lock (_lock) {
            if (_process == null || _process.IsCompleted) {
                _process = StartWorking(cancellationToken);
            }
        }
    }

    private async Task? StartWorking(CancellationToken cancellationToken) {
        while (await _reader.ReadNextPacket(cancellationToken) is {

        } packet)
            try {
                await HandlePacket(packet, cancellationToken);
            } catch (OperationCanceledException) {
                return;
            } catch (Exception ex) {
                if (_logger.IsEnabled(LogLevel.Critical))
                    _logger.LogCritical("Received exception {Ex}", ex);

                await End(1, cancellationToken);
                return;
            }
    }

    private Task End(uint statusCode, CancellationToken cancellationToken) {
        return _client.Exit(statusCode, cancellationToken);
    }

    private Task HandlePacket(Packet payload, CancellationToken cancellationToken) {
        if (!_isInitialized) return Initialize(payload, cancellationToken);
        return HandleGenericPacket(payload, cancellationToken);
    }
    private Task ReturnPacket(ISftpServerPayload packet, CancellationToken cancellationToken)
        => _client.ReturnPacket(packet.ToPacket().ToByteString(), cancellationToken);

    private async Task HandleGenericPacket(Packet payload, CancellationToken cancellationToken) {
        var type = payload.PacketType;
        switch (type) {
            case var t when t.IsServerSideMessage() || t.IsInitMessage(): {
                    await _client.Exit(1, cancellationToken);
                    break;
                }
            default: {
                    var idBytes = payload.Bytes.AsSpan(1, 4);
                    if (!uint.FromSsh(idBytes, out var id))
                        await End(2, cancellationToken);

                    var response = new Status(id, SftpError.OpUnsupported, $"We don't support {type}");
                    await _client.ReturnPacket(response.ToPacket().ToByteString(), cancellationToken);
                    return;
                }

        }
    }

    private async Task Unparsable(string v, CancellationToken cancellationToken) {
        throw new NotImplementedException();
    }

    static readonly ImmutableList<SftpExtension> SupportedExtensions = [
        new SftpExtension.LSetStat()
    ];
    private readonly Lock _lock = new();

    private Task Initialize(Packet payload, CancellationToken cancellationToken) {
        if (!Init.TryParse(payload.Bytes, out _)) {
            return End(2, cancellationToken);
        }
        var versionPacket = new Version(3, SupportedExtensions);
        _isInitialized = true;
        return _client.ReturnPacket(versionPacket.ToPacket().ToByteString(), cancellationToken);
    }
}

interface IChannelClient {
    Task End(Disconnect disconnect, CancellationToken cancellationToken);
    Task ReturnPacket(byte[] packet, CancellationToken cancellationToken);
    Task Exit(uint StatusCode, CancellationToken cancellationToken);
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
