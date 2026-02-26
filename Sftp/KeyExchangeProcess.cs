// KeyExchangeProcess.cs - Part of the ZipZap project for storing files online
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
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using ZipZap.Sftp.Ssh;
using ZipZap.Sftp.Ssh.Algorithms;

namespace ZipZap.Sftp;

public class KeyExchangeProcess {
    private readonly IProvider<IKeyExchangeAlgorithm> _kexProvider;
    private readonly IProvider<IEncryptionAlgorithm> _encryptionProvider;
    private readonly IProvider<IServerHostKeyAlgorithm> _serverHostKeyProvider;
    private readonly IProvider<ICompressionAlgorithm> _compressionProvider;
    private readonly IProvider<IMacAlgorithm> _macProvider;
    private readonly ILogger<KeyExchangeProcess> _logger;
    public KeyExchangeProcess(
        IProvider<IKeyExchangeAlgorithm> KexProvider,
        IProvider<IEncryptionAlgorithm> EncryptionProvider,
        IProvider<IServerHostKeyAlgorithm> ServerHostKeyProvider,
        IProvider<ICompressionAlgorithm> CompressionProvider,
        IProvider<IMacAlgorithm> MacProvider,
        ILogger<KeyExchangeProcess> Logger
    ) {
        _kexProvider = KexProvider;
        _encryptionProvider = EncryptionProvider;
        _serverHostKeyProvider = ServerHostKeyProvider;
        _compressionProvider = CompressionProvider;
        _macProvider = MacProvider;
        _logger = Logger;
    }

    public async Task<SshState?> MakeKeyExchange(KeyExchangeInput input, Packet? kexInit, CancellationToken cancellationToken) {
        var keyExchangeAlgorithms = _kexProvider.Items;
        var publicKeyAlgorithms = _serverHostKeyProvider.Items;
        var encryptionAlgorithms = _encryptionProvider.Items;
        var macAlgorithms = _macProvider.Items;
        var compressionAlgorithms = _compressionProvider.Items;

        var reader = input.Reader;

        var keyExchangePacket = GenerateKeyExchangePacket(keyExchangeAlgorithms, publicKeyAlgorithms, encryptionAlgorithms, macAlgorithms, compressionAlgorithms, false);
        await reader.SendPacket(keyExchangePacket, cancellationToken);

        byte[] clientKexPayloadRaw; KeyExchange? clientKexPayload;

        if (kexInit is null) {
            var clientkextuple = await reader.ReadUntilPacket<KeyExchange>(cancellationToken);
            if (clientkextuple is null) return null;
            (var packet, clientKexPayload) = clientkextuple.Value;
            clientKexPayloadRaw = packet.Payload;
        } else {
            if (!KeyExchange.TryParse(kexInit.Payload, out clientKexPayload)) return null;
            clientKexPayloadRaw = kexInit.Payload;
        }

        var serverKexPayload = keyExchangePacket.ToPayload();
        var algs = GetAlgorithmsFromKexInit(clientKexPayload, keyExchangePacket);
        if (algs is null) return null;

        var kexResult = await algs.KeyExchangeAlgorithm.ExchangeKeysAsync(
            algs.PublicKeyAlgorithm.GetHostKeyPair(),
            new(clientKexPayloadRaw, serverKexPayload),
            input,
            cancellationToken
        );

        if (kexResult is not (var secret, var exchangeHash))
            return null;

        var newKeysPacket = new NewKeys();
        var clientNewKeys = await reader.ReadUntilPacket<NewKeys>(cancellationToken);
        if (clientNewKeys is null) return null;

        await reader.SendPacket(newKeysPacket, cancellationToken);

        var keys = GenerateKeys(secret, exchangeHash, exchangeHash, algs.KeyExchangeAlgorithm);
        var (newEncryptor, newDecryptor) = GenerateEncryptors(input, algs, keys);
        var sshState = new SshState(
            input.SessionId ?? exchangeHash,
            newEncryptor,
            newDecryptor,
            clientKexPayloadRaw,
            serverKexPayload
        );
        return sshState;
    }
    private static (IEncryptor, IDecryptor) GenerateEncryptors(KeyExchangeInput input, AlgorithmCollection algs, EncryptionKeys keys) {
        var newMacGenerator = algs.MacAlgorithmStC.CreateGenerator(
            input.Reader.SequentialStC,
            keys.IntegrityStC[..algs.MacAlgorithmStC.KeyLength]
        );

        var newEncryptor = algs.EncAlgorithmStC.GetEncryptor(
            input.Stream,
            keys.IvStC[..algs.EncAlgorithmStC.IVLength],
            keys.EncryptionStC[..algs.EncAlgorithmStC.KeyLength],
            newMacGenerator
        );
        var newMacValidator = algs.MacAlgorithmCtS.CreateValidator(
            input.Reader.SequentialCtS,
            keys.IntegrityCtS[..algs.MacAlgorithmCtS.KeyLength]
        );

        var newDecryptor = algs.EncAlgorithmCtS.GetDecryptor(
            input.Stream,
            keys.IvCtS[..algs.EncAlgorithmCtS.IVLength],
            keys.EncryptionCtS[..algs.EncAlgorithmCtS.KeyLength],
            newMacValidator
        );
        return (newEncryptor, newDecryptor);
    }
    private AlgorithmCollection? GetAlgorithmsFromKexInit(KeyExchange clientKexPayload, KeyExchange serverKexPayload) {

        var keyExchangeAlgorithmName = clientKexPayload.KexAlgorithms.Names.FirstOrDefault(a => Enumerable.Contains(serverKexPayload.KexAlgorithms.Names, a));
        if (keyExchangeAlgorithmName is null) {
            if (_logger.IsEnabled(LogLevel.Information)) {
                _logger.LogInformation("no key exchange algorithm");
                _logger.LogInformation("our   : {KexAlgorithms}", serverKexPayload.KexAlgorithms);
                _logger.LogInformation("their : {KexAlgorithms}", clientKexPayload.KexAlgorithms);
            }
            return null;
        }

        var keyExchangeAlgorithm = _kexProvider.Items.FirstOrDefault(a => a.Name == keyExchangeAlgorithmName)!;
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("using keyExchangeAlgorithm {KeyExchangeAlgorithm}", keyExchangeAlgorithm);

        var cts = GetEncryptionAndMac(
            clientKexPayload,
            serverKexPayload,
            k => k.MacAlgorithmsCtS,
            k => k.EncryptionAlgorithmsCtS);
        if (cts is null) return null;
        var (encCtS, macCtS) = cts.Value;

        var stc = GetEncryptionAndMac(
            clientKexPayload,
            serverKexPayload,
            k => k.MacAlgorithmsStC,
            k => k.EncryptionAlgorithmsStC);
        if (stc is null) return null;
        var (encStC, macStC) = cts.Value;

        var publicKeyAlgorithmName = clientKexPayload.ServerHostKeyAlgorithms.Names.FirstOrDefault(a => Enumerable.Contains(serverKexPayload.ServerHostKeyAlgorithms.Names, a));
        if (publicKeyAlgorithmName is null) return null;

        var publicKeyAlgorithm = _serverHostKeyProvider.Items.FirstOrDefault(a => a.Name == publicKeyAlgorithmName)!;

        if (_logger.IsEnabled(LogLevel.Information)) {
            _logger.LogInformation("Using PublicKeyAlgorithm {PublicKeyAlgorithm}", publicKeyAlgorithm);
            _logger.LogInformation("Using MacAlgorithm {MacAlgorithm} for sending and {CtS} for reading", macStC, macCtS);
            _logger.LogInformation("Using encryptionAlgorithm {EncStc} for sending and {CtS} for reading", encStC, encCtS);
            _logger.LogInformation("sending KeyExchangeAlgorithm {KeyExchangeAlgorithm}", keyExchangeAlgorithm);
        }
        return new(
            keyExchangeAlgorithm,
            macCtS,
            encCtS,
            macStC,
            encStC,
            publicKeyAlgorithm
        );
    }

    private (IEncryptionAlgorithm, IMacAlgorithm)? GetEncryptionAndMac(
        KeyExchange client,
        KeyExchange server,
        Func<KeyExchange, NameList> MacList,
        Func<KeyExchange, NameList> EncList) {

        var encCtSName = EncList(client).Names.FirstOrDefault(a => server.EncryptionAlgorithmsCtS.Names.Contains(a));
        if (encCtSName is null) {
            if (_logger.IsEnabled(LogLevel.Information)) {
                _logger.LogInformation("no mac algorithm");
                _logger.LogInformation("our   : {MacAlgorithm}", server.MacAlgorithmsCtS);
                _logger.LogInformation("their : {MacAlgorithm}", client.MacAlgorithmsCtS);
            }
            return null;
        }
        var encAlgorithm = _encryptionProvider.Items.FirstOrDefault(a => a.Name == encCtSName)!;

        IMacAlgorithm macAlgorithm = new NoMacAlgorithm();
        if (encAlgorithm.OverridesMac) {
            if (_logger.IsEnabled(LogLevel.Information)) {
                _logger.LogInformation("got {encAlgorithm} algorithm, no Mac needed. Skipping.", encAlgorithm);
            }
        } else {
            var macAlgorithmCtSName = MacList(client).Names.FirstOrDefault(a => Enumerable.Contains(server.MacAlgorithmsCtS.Names, a));
            if (macAlgorithmCtSName is null) {
                if (_logger.IsEnabled(LogLevel.Information)) {
                    _logger.LogInformation("no mac algorithm");
                    _logger.LogInformation("our   : {MacAlgorithm}", server.MacAlgorithmsCtS);
                    _logger.LogInformation("their : {MacAlgorithm}", client.MacAlgorithmsCtS);
                }
                return null;
            }

            macAlgorithm = _macProvider.Items.FirstOrDefault(a => a.Name == macAlgorithmCtSName)!;
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("using macAlgorithm {MacAlgorithm}", macAlgorithm);
        }
        return (encAlgorithm, macAlgorithm);
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
    private static EncryptionKeys GenerateKeys(BigInteger secret, byte[] exchangeHash, byte[] sessionId, IKeyExchangeAlgorithm keyExchangeAlgorithm) {
        var hashes = Enumerable.Range('A', 6)
            .Select(ch => HashWithParameter(
                secret,
                exchangeHash,
                (byte)ch,
                sessionId,
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
    private static byte[] HashWithParameter(BigInteger secret, byte[] exchangeHash, byte parameter, byte[] sessionId, IKeyExchangeAlgorithm keyExchangeAlgorithm) {
        return keyExchangeAlgorithm.Hash(
            new SshMessageBuilder()
                .Write(secret)
                .WriteArray(exchangeHash)
                .Write(parameter)
                .WriteArray(sessionId).Build()
        );
    }

}
