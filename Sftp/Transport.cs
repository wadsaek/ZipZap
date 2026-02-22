// Transport.cs - Part of the ZipZap project for storing files online
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
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using ZipZap.Sftp.Ssh;
using ZipZap.Sftp.Ssh.Algorithms;
using ZipZap.Sftp.Ssh.Numbers;

namespace ZipZap.Sftp;

internal class Transport {
    private readonly IProvider<IKeyExchangeAlgorithm> _kexProvider;
    private readonly IProvider<IEncryptionAlgorithm> _encryptionProvider;
    private readonly IProvider<IServerHostKeyAlgorithm> _serverHostKeyProvider;
    private readonly IProvider<ICompressionAlgorithm> _compressionProvider;
    private readonly IProvider<IMacAlgorithm> _macProvider;

    private readonly ILogger<Transport> _logger;
    private readonly IPacketHandlerFactory _handler;

    public Transport(
        ILogger<Transport> logger,
        IProvider<IKeyExchangeAlgorithm> kexProvider,
        IProvider<IEncryptionAlgorithm> encryptionProvider,
        IProvider<IServerHostKeyAlgorithm> serverHostKeyProvider,
        IProvider<ICompressionAlgorithm> compressionProvider,
        IProvider<IMacAlgorithm> macProvider,
        IPacketHandlerFactory handler
        ) {
        _logger = logger;
        _kexProvider = kexProvider;
        _encryptionProvider = encryptionProvider;
        _serverHostKeyProvider = serverHostKeyProvider;
        _compressionProvider = compressionProvider;
        _macProvider = macProvider;
        _handler = handler;
    }

    private class PacketSender : IPacketSender {
        public void SendPacket<T>(T Packet) where T : IServerPayload {
            throw new NotImplementedException();
        }
    }
    public async Task Handle(Stream stream, IdenitificationStrings idenitificationStrings, CancellationToken cancellationToken) {
        var state = await MakeKeyExchange(stream, idenitificationStrings, cancellationToken);
        if (state is null) return;
        var handler = _handler.Create(new PacketSender());
        var tokenSource = new CancellationTokenSource();
        // tokenSource.th
        while (await state.Decryptor.ReadPacket(cancellationToken) is Packet packet) {
        }

        var disconnect = new Disconnect(DisconnectCode.ProtocolError, "Unable to parse packet");
        var disconnectPacket = await state.Encryptor.EncryptPacket(disconnect, cancellationToken);
        await state.Stream.SshWriteArray(disconnectPacket, cancellationToken);
        return;
    }
    private async Task<SshState?> MakeKeyExchange(Stream stream, IdenitificationStrings idenitificationStrings, CancellationToken cancellationToken) {
        var keyExchangeAlgorithms = _kexProvider.Items;
        var publicKeyAlgorithms = _serverHostKeyProvider.Items;
        var encryptionAlgorithms = _encryptionProvider.Items;
        var macAlgorithms = _macProvider.Items;
        var compressionAlgorithms = _compressionProvider.Items;
        var macGenerator = new NoMacAlgorithm().CreateGenerator(0, []);
        var macValidator = new NoMacAlgorithm().CreateValidator(0, []);
        var packetReader = new NoEncryptionAlgorithm().GetDecryptor(stream, [], [], macValidator);
        var packetEncoder = new NoEncryptionAlgorithm().GetEncryptor([], [], macGenerator);

        Packet? clientKexPayloadRaw;
        KeyExchange? clientKexPayload = null;
        do {
            clientKexPayloadRaw = await packetReader.ReadPacket(cancellationToken);
            if (clientKexPayloadRaw is null) return null;
            if (!KeyExchange.TryParse(clientKexPayloadRaw.Payload, out clientKexPayload)) {
                if (HandleGenericTransportMsg(clientKexPayloadRaw) is string description) {
                    var packet = new Disconnect(ReasonCode: DisconnectCode.ProtocolError, description);
                    var payload = await packetEncoder.EncryptPacket(packet, cancellationToken);
                    await stream.SshWriteArray(payload, cancellationToken);
                }
            }
        }
        while (clientKexPayload == null);
        var keyExchangePacket = GenerateKeyExchangePacket(keyExchangeAlgorithms, publicKeyAlgorithms, encryptionAlgorithms, macAlgorithms, compressionAlgorithms, false);
        var serverKexPayload = keyExchangePacket.ToPayload();
        byte[] rawKexPacket = await packetEncoder.EncryptPacket(keyExchangePacket, cancellationToken);
        await stream.SshWriteArray(rawKexPacket, cancellationToken);

        var keyExchangeAlgorithmName = clientKexPayload.KexAlgorithms.Names.FirstOrDefault(a => Enumerable.Contains(keyExchangePacket.KexAlgorithms.Names, a));
        if (keyExchangeAlgorithmName is null) {
            if (_logger.IsEnabled(LogLevel.Information)) {
                _logger.LogInformation("no key exchange algorithm");
                _logger.LogInformation("our   : {KexAlgorithms}", keyExchangePacket.KexAlgorithms);
                _logger.LogInformation("their : {KexAlgorithms}", clientKexPayload.KexAlgorithms);
            }
            return null;
        }

        var keyExchangeAlgorithm = keyExchangeAlgorithms.FirstOrDefault(a => a.Name == keyExchangeAlgorithmName)!;
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("using keyExchangeAlgorithm {KeyExchangeAlgorithm}", keyExchangeAlgorithm);


        var encCtSName = clientKexPayload.EncryptionAlgorithmsCtS.Names.FirstOrDefault(a => keyExchangePacket.EncryptionAlgorithmsCtS.Names.Contains(a));
        if (encCtSName is null) {
            if (_logger.IsEnabled(LogLevel.Information)) {
                _logger.LogInformation("no mac algorithm");
                _logger.LogInformation("our   : {MacAlgorithm}", keyExchangePacket.MacAlgorithmsCtS);
                _logger.LogInformation("their : {MacAlgorithm}", clientKexPayload.MacAlgorithmsCtS);
            }
            return null;
        }
        var encAlgorithm = encryptionAlgorithms.FirstOrDefault(a => a.Name == encCtSName)!;
        IMacAlgorithm macAlgorithm = new NoMacAlgorithm();
        if (encAlgorithm.OverridesMac) {
            if (_logger.IsEnabled(LogLevel.Information)) {
                _logger.LogInformation("got {encAlgorithm} algorithm, no Mac needed. Skipping.", encAlgorithm);
            }
        } else {
            var macAlgorithmCtSName = clientKexPayload.MacAlgorithmsCtS.Names.FirstOrDefault(a => Enumerable.Contains(keyExchangePacket.MacAlgorithmsCtS.Names, a));
            if (macAlgorithmCtSName is null) {
                if (_logger.IsEnabled(LogLevel.Information)) {
                    _logger.LogInformation("no mac algorithm");
                    _logger.LogInformation("our   : {MacAlgorithm}", keyExchangePacket.MacAlgorithmsCtS);
                    _logger.LogInformation("their : {MacAlgorithm}", clientKexPayload.MacAlgorithmsCtS);
                }
                return null;
            }

            macAlgorithm = macAlgorithms.FirstOrDefault(a => a.Name == macAlgorithmCtSName)!;
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("using macAlgorithm {MacAlgorithm}", macAlgorithm);
        }

        var publicKeyAlgorithmName = clientKexPayload.ServerHostKeyAlgorithms.Names.FirstOrDefault(a => Enumerable.Contains(keyExchangePacket.ServerHostKeyAlgorithms.Names, a));
        if (publicKeyAlgorithmName is null) return null;

        var publicKeyAlgorithm = publicKeyAlgorithms.FirstOrDefault(a => a.Name == publicKeyAlgorithmName)!;

        if (_logger.IsEnabled(LogLevel.Information)) {
            _logger.LogInformation("Using PublicKeyAlgorithm {PublicKeyAlgorithm}", publicKeyAlgorithm);
            _logger.LogInformation("Using MacAlgorithm {MacAlgorithm}", macAlgorithm);
            _logger.LogInformation("sending KeyExchangeAlgorithm {KeyExchangeAlgorithm}", keyExchangeAlgorithm);
        }
        var sshState = new SshState(
            [],
            stream,
            0,
            [],
            packetEncoder,
            packetReader,
            keyExchangeAlgorithm,
            idenitificationStrings,
            publicKeyAlgorithm.GetHostKeyPair(),
            clientKexPayloadRaw.Payload,
            serverKexPayload
        );
        if (await keyExchangeAlgorithm.ExchangeKeysAsync(sshState, cancellationToken)
            is not KeyExchangeResult(var secret, var exchangeHash))
            return null;
        var newKeysPacket = new NewKeys();
        var newKeysBytes = await packetEncoder.EncryptPacket(newKeysPacket, cancellationToken);
        await sshState.Stream.SshWriteArray(newKeysBytes, cancellationToken);
        var keys = GenerateInitialKeys(secret, exchangeHash, sshState.KeyExchangeAlgorithm);
        sshState = sshState with {
            SessionId = exchangeHash,
            LastExchangeHash = exchangeHash,
            Secret = secret,
        };
        return sshState;
    }

    private string? HandleGenericTransportMsg(Packet clientKexPayloadRaw) {
        using var stream = new MemoryStream(clientKexPayloadRaw.Payload);
        if (!stream.SshTryReadByteSync(out var msg)) msg = 0; ;

        switch ((Message)msg) {
            case Message.Ignore: return null;
            case Message.Disconnect: {

                    if (!stream.SshTryReadUint32Sync(out var reasonRaw)) return "";
                    var reason = (DisconnectCode)reasonRaw;
                    if (!stream.SshTryReadStringSync(out var description)) description = "";
                    _ = stream.SshTryReadStringSync(out _);
                    if (_logger.IsEnabled(LogLevel.Information))
                        _logger.LogInformation("Got a disconnect {Code} with description {description}", reason, description);
                    return description;
                }
            case Message.Debug: {
                    if (!stream.SshTryReadBoolSync(out var display)) display = false;
                    if (!stream.SshTryReadStringSync(out var description)) description = "";
                    _ = stream.SshTryReadStringSync(out _);
                    if (display && _logger.IsEnabled(LogLevel.Debug))
                        _logger.LogDebug("Got a debug message with description {description}", description);

                    return null;
                }
            case Message.Unimplemented: {
                    if (!stream.SshTryReadUint32Sync(out _)) {
                        return "Can't read the message type of an unimplemented message";
                    }
                    return null;
                }
            default: {
                    return null;
                }
        }
    }

    private static KeyExchange GenerateKeyExchangePacket(IEnumerable<IKeyExchangeAlgorithm> keyExchangeAlgorithms, IEnumerable<IServerHostKeyAlgorithm> serverHostKeyAlgorithms, IEnumerable<IEncryptionAlgorithm> encryptionAlgorithms, IEnumerable<IMacAlgorithm> macAlgorithms, IEnumerable<ICompressionAlgorithm> compressionAlgorithms, bool firstPacketFollows) {
        var cookie = RandomNumberGenerator.GetBytes(16);
        var encryptionAlgorithmsList = encryptionAlgorithms.ToNameList();
        var macAlgorithmsNameList = macAlgorithms.ToNameList();
        var compressionAlgorithmsNameList = compressionAlgorithms.ToNameList();
        return new(
            cookie,
            keyExchangeAlgorithms.ToNameList(),
            serverHostKeyAlgorithms.ToNameList(),
            encryptionAlgorithmsList,
            encryptionAlgorithmsList,
            macAlgorithmsNameList,
            macAlgorithmsNameList,
            compressionAlgorithmsNameList,
            compressionAlgorithmsNameList,
            new([]),
            new([]),
            firstPacketFollows,
            0
        );
    }
    static EncryptionKeys GenerateInitialKeys(BigInteger secret, byte[] exchangeHash, IKeyExchangeAlgorithm keyExchangeAlgorithm) {
        var hashes = Enumerable.Range('A', 6)
            .Select(ch => HashWithParameter(
                secret,
                exchangeHash,
                (byte)ch,
                exchangeHash,
                keyExchangeAlgorithm))
            .ToList();
        return new(
            hashes[0],
            hashes[1],
            hashes[2],
            hashes[3],
            hashes[4],
            hashes[5]
        );

    }
    static byte[] HashWithParameter(BigInteger secret, byte[] exchangeHash, byte parameter, byte[] sessionId, IKeyExchangeAlgorithm keyExchangeAlgorithm) {
        return keyExchangeAlgorithm.Hash(
            new SshMessageBuilder()
                .Write(secret)
                .WriteArray(exchangeHash)
                .Write(parameter)
                .WriteArray(sessionId).Build()
        );
    }
}

internal record EncryptionKeys(byte[] IVCtS, byte[] IvStC, byte[] EncryptionCtS, byte[] EncryptionStC, byte[] IntegrityCtS, byte[] IntegrityStC) { }

public record IdenitificationStrings(string Server, string Client);

public record SessionId(byte[] Value) {
    public override string ToString() {
        return Convert.ToBase64String(Value);
    }
}
