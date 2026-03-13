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
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Received packet of type {Type}", type);
        switch (type) {
            case var t when t.IsServerSideMessage() || t.IsInitMessage(): {
                    await _client.Exit(1, cancellationToken);
                    break;
                }
            case Message.Open: {
                    if (!Open.TryParse(payload.Bytes, out var open)) {
                        await Unparsable(nameof(Open), cancellationToken);
                        return;
                    }
                    var result = await _handler.Open(open.Filename, open.Flags, open.Attrs, cancellationToken);
                    var packet = result
                        .Select(handle => new Handle(open.Id, handle.Value) as ISftpServerPayload)
                        .UnwrapOrElse(status => status.ToStatusPacket(open.Id));
                    await ReturnPacket(packet, cancellationToken);
                    return;

                }
            case Message.Close: {
                    if (!Close.TryParse(payload.Bytes, out var close)) {
                        await Unparsable(nameof(Close), cancellationToken);
                        return;
                    }
                    var result = await _handler.Close(new(close.Handle), cancellationToken);
                    await ReturnPacket(result.ToStatusPacket(close.Id), cancellationToken);
                    return;
                }
            case Message.Read: {
                    if (!Read.TryParse(payload.Bytes, out var read)) {
                        await Unparsable(nameof(Read), cancellationToken);
                        return;
                    }
                    var result = await _handler.Read(new(read.Handle), read.Offset, read.Length, cancellationToken);
                    var packet = result
                        .Select(data => new Data(read.Id, data) as ISftpServerPayload)
                        .UnwrapOrElse(status => status.ToStatusPacket(read.Id));
                    await _client.ReturnPacket(packet.ToPacket().ToByteString(), cancellationToken);
                    return;
                }
            case Message.Write: {
                    if (!Write.TryParse(payload.Bytes, out var write)) {
                        await Unparsable(nameof(Write), cancellationToken);
                        return;
                    }
                    var result = await _handler.Write(new(write.Handle), write.Offset, write.Data, cancellationToken);
                    await ReturnPacket(result.ToStatusPacket(write.Id), cancellationToken);
                    return;
                }
            case Message.Lstat: {
                    if (!Lstat.TryParse(payload.Bytes, out var lstat)) {
                        await Unparsable(nameof(Lstat), cancellationToken);
                        return;
                    }
                    var result = await _handler.LStat(lstat.Path, cancellationToken);
                    var packet = result
                        .Select(attrs => new Attrs(lstat.Id, attrs) as ISftpServerPayload)
                        .UnwrapOrElse(status => status.ToStatusPacket(lstat.Id));
                    await ReturnPacket(packet, cancellationToken);
                    return;
                }
            case Message.Fstat: {
                    if (!Fstat.TryParse(payload.Bytes, out var fstat)) {
                        await Unparsable(nameof(Fstat), cancellationToken);
                        return;
                    }
                    var result = await _handler.FStat(new(fstat.Handle), cancellationToken);
                    var packet = result
                        .Select(attrs => new Attrs(fstat.Id, attrs) as ISftpServerPayload)
                        .UnwrapOrElse(status => status.ToStatusPacket(fstat.Id));
                    await ReturnPacket(packet, cancellationToken);
                    return;
                }
            case Message.SetStat: {
                    if (!SetStat.TryParse(payload.Bytes, out var setstat)) {
                        await Unparsable(nameof(SetStat), cancellationToken);
                        return;
                    }
                    var result = await _handler.SetStat(setstat.Path, setstat.Attrs, cancellationToken);
                    await ReturnPacket(result.ToStatusPacket(setstat.Id), cancellationToken);
                    return;
                }
            case Message.FsetStat: {
                    if (!FsetStat.TryParse(payload.Bytes, out var fsetstat)) {
                        await Unparsable(nameof(FsetStat), cancellationToken);
                        return;
                    }
                    var result = await _handler.FSetStat(new(fsetstat.Handle), fsetstat.Attrs, cancellationToken);
                    await ReturnPacket(result.ToStatusPacket(fsetstat.Id), cancellationToken);
                    return;
                }
            case Message.OpenDir: {
                    if (!OpenDir.TryParse(payload.Bytes, out var openDir)) {
                        await Unparsable(nameof(OpenDir), cancellationToken);
                        return;
                    }
                    var result = await _handler.OpenDir(openDir.Path, cancellationToken);
                    var packet = result
                        .Select(attrs => new Handle(openDir.Id, attrs.Value) as ISftpServerPayload)
                        .UnwrapOrElse(status => status.ToStatusPacket(openDir.Id));
                    await ReturnPacket(packet, cancellationToken);
                    return;
                }
            case Message.ReadDir: {
                    if (!ReadDir.TryParse(payload.Bytes, out var readDir)) {
                        await Unparsable(nameof(ReadDir), cancellationToken);
                        return;
                    }
                    var result = await _handler.ReadDir(new(readDir.Handle), cancellationToken);
                    var packet = result
                        .Select(names => new Name(readDir.Id, names.ToImmutableList()) as ISftpServerPayload)
                        .UnwrapOrElse(status => status.ToStatusPacket(readDir.Id));
                    await ReturnPacket(packet, cancellationToken);
                    return;
                }
            case Message.Remove: {
                    if (!Remove.TryParse(payload.Bytes, out var remove)) {
                        await Unparsable(nameof(Remove), cancellationToken);
                        return;
                    }
                    var result = await _handler.Remove(remove.Filename, cancellationToken);
                    await ReturnPacket(result.ToStatusPacket(remove.Id), cancellationToken);
                    return;
                }
            case Message.Mkdir: {
                    if (!Mkdir.TryParse(payload.Bytes, out var mkdir)) {
                        await Unparsable(nameof(Mkdir), cancellationToken);
                        return;
                    }
                    var result = await _handler.MkDir(mkdir.Path, mkdir.Attrs, cancellationToken);
                    await ReturnPacket(result.ToStatusPacket(mkdir.Id), cancellationToken);
                    return;
                }
            case Message.Rmdir: {
                    if (!Rmdir.TryParse(payload.Bytes, out var rmdir)) {
                        await Unparsable(nameof(Rmdir), cancellationToken);
                        return;
                    }
                    var result = await _handler.RmDir(rmdir.Path, cancellationToken);
                    await ReturnPacket(result.ToStatusPacket(rmdir.Id), cancellationToken);
                    return;
                }
            case Message.Realpath: {
                    if (!Realpath.TryParse(payload.Bytes, out var realpath)) {
                        await Unparsable(nameof(Realpath), cancellationToken);
                        return;
                    }
                    var result = await _handler.RealPath(realpath.Path, cancellationToken);
                    var packet = result
                        .Select(name => new Name(realpath.Id, [name]) as ISftpServerPayload)
                        .UnwrapOrElse(status => status.ToStatusPacket(realpath.Id));
                    await ReturnPacket(packet, cancellationToken);
                    return;
                }
            case Message.Stat: {
                    if (!Stat.TryParse(payload.Bytes, out var stat)) {
                        await Unparsable(nameof(Stat), cancellationToken);
                        return;
                    }
                    var result = await _handler.Stat(stat.Path, cancellationToken);
                    var packet = result
                        .Select(attrs => new Attrs(stat.Id, attrs) as ISftpServerPayload)
                        .UnwrapOrElse(status => status.ToStatusPacket(stat.Id));
                    await ReturnPacket(packet, cancellationToken);
                    return;
                }
            case Message.Rename: {
                    if (!Rename.TryParse(payload.Bytes, out var rename)) {
                        await Unparsable(nameof(Rename), cancellationToken);
                        return;
                    }
                    var result = await _handler.Rename(rename.OldPath, rename.NewPath, cancellationToken);
                    await ReturnPacket(result.ToStatusPacket(rename.Id), cancellationToken);
                    return;
                }
            case Message.ReadLink: {
                    if (!ReadLink.TryParse(payload.Bytes, out var readlink)) {
                        await Unparsable(nameof(ReadLink), cancellationToken);
                        return;
                    }
                    var result = await _handler.Readlink(readlink.Path, cancellationToken);
                    var packet = result
                        .Select(name => new Name(readlink.Id, [name]) as ISftpServerPayload)
                        .UnwrapOrElse(status => status.ToStatusPacket(readlink.Id));
                    await ReturnPacket(packet, cancellationToken);
                    return;
                }
            case Message.Symlink: {
                    if (!Symlink.TryParse(payload.Bytes, out var symlink)) {
                        await Unparsable(nameof(Symlink), cancellationToken);
                        return;
                    }
                    var result = await _handler.Symlink(symlink.LinkPath, symlink.Target, cancellationToken);
                    await ReturnPacket(result.ToStatusPacket(symlink.Id), cancellationToken);
                    return;
                }
            // TODO: add extensions
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

    private Task Unparsable(string packetType, CancellationToken cancellationToken) {
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("failed to parse packet {PacketType}", packetType);
        return _client.Exit(2, cancellationToken);
    }

    static readonly ImmutableList<SftpExtensionDeclaration> SupportedExtensions = [
        new SftpExtensionDeclaration.LSetStat()
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
