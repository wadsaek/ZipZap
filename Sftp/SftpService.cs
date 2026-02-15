// SftpService.cs - Part of the ZipZap project for storing files online
//     Copyright (C) 2026  Barenboim Esther wadsaek@gmail.com
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using ZipZap.Sftp.Ssh;
using ZipZap.Sftp.Ssh.Algorithms;

namespace ZipZap.Sftp;

internal static class TaskListExt {
    extension(List<Task> tasks) {
        public async Task<int> RemoveCompleted() {
            foreach (var a in tasks.Where(t => t.IsCompleted))
                await a;
            return tasks.RemoveAll(t => t.IsCompleted);
        }
    }
}
public partial class SftpService : BackgroundService {
    private readonly ISftpRequestHandler _handler;
    private readonly ISftpConfiguration _configuration;
    private readonly ILogger<SftpService> _logger;
    private readonly IServiceScopeFactory _factory;

    public SftpService(ISftpRequestHandler handler, ISftpConfiguration configuration, ILogger<SftpService> logger, IServiceScopeFactory factory) {
        _handler = handler;
        _configuration = configuration;
        _logger = logger;
        _factory = factory;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken) {
        var listener = new TcpListener(IPAddress.Any, _configuration.Port);
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("listening on port {Port}", _configuration.Port);
        var tasks = new List<Task>();
        listener.Start();
        while (!cancellationToken.IsCancellationRequested) {
            var socket = await listener.AcceptSocketAsync(cancellationToken);
            tasks.Add(HandleSocket(socket, cancellationToken));
            var removed = await tasks.RemoveCompleted();
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("{Count} tasks in queue\n{Removed} tasks removed", tasks.Count, removed);
        }
    }
    private async Task HandleSocket(Socket socket, CancellationToken cancellationToken) {
        try {
            using var _ = socket;
            await using var scope = _factory.CreateAsyncScope();
            await using var stream = new NetworkStream(socket);

            var idStrings = await ExchangeIdentificationStrings(stream, cancellationToken);

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("handling client {ClientId}", idStrings.Client);

            var state = await MakeKeyExchange(scope, stream, idStrings, cancellationToken);
            await Task.Delay(TimeSpan.FromMinutes(2), cancellationToken);

        } catch (OperationCanceledException e) {
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("Operation was cancelled: {e}", e.Message);
        } catch (IOException e) {
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("IOException encountered: {e}", e.Message);
        }
    }

    private async Task<IdenitificationStrings> ExchangeIdentificationStrings(Stream stream, CancellationToken cancellationToken) {
        var serverHeader = $"SSH-2.0-{_configuration.ServerName}_{_configuration.Version}";
        var header = Encoding.ASCII.GetBytes(serverHeader);
        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(Encoding.ASCII.GetBytes("\r\n"), cancellationToken);

        var clientHeaderBytes = new List<byte>();
        while (clientHeaderBytes.Count < 2 || (clientHeaderBytes[^2] != (byte)'\r' && clientHeaderBytes[^1] != (byte)'\n'))
            clientHeaderBytes.Add((byte)stream.ReadByte());
        clientHeaderBytes.RemoveAt(clientHeaderBytes.Count - 1);
        clientHeaderBytes.RemoveAt(clientHeaderBytes.Count - 1);
        var clientHeader = Encoding.ASCII.GetString(clientHeaderBytes.ToArray());
        return new(serverHeader, clientHeader);
    }

    private async Task<SshState?> MakeKeyExchange(AsyncServiceScope scope, Stream stream, IdenitificationStrings idenitificationStrings, CancellationToken cancellationToken) {
        var provider = scope.ServiceProvider;
        IProvider<T> GetProvider<T>() where T : INamed => provider.GetRequiredService<IProvider<T>>();
        var keyExchangeAlgorithms = GetProvider<IKeyExchangeAlgorithm>().Items;
        var publicKeyAlgorithms = GetProvider<IServerHostKeyAlgorithm>().Items;
        var encryptionAlgorithms = GetProvider<IEncryptionAlgorithm>().Items;
        var macAlgorithms = GetProvider<IMacAlgorithm>().Items;
        var compressionAlgorithms = GetProvider<ICompressionAlgorithm>().Items;

        var clientKexPayloadRaw = await stream.SshTryReadPacket(new NoMacAlgorithm(), cancellationToken);
        if (clientKexPayloadRaw is null) return null;
        var clientKexPayload = await KeyExchange.TryFromPayload(clientKexPayloadRaw.Inner.Payload, cancellationToken);
        if (clientKexPayload is null) return null;
        var serverKexPayload = GenerateKeyExchangePacket(keyExchangeAlgorithms, publicKeyAlgorithms, encryptionAlgorithms, macAlgorithms, compressionAlgorithms, false);
        var keyExchangePacket = await serverKexPayload.ToPacket(new NoMacAlgorithm(), default, default, cancellationToken);
        await stream.SshWritePacket(keyExchangePacket, cancellationToken);

        var keyExchangeAlgorithmName = clientKexPayload.KexAlgorithms.Names.FirstOrDefault(a => serverKexPayload.KexAlgorithms.Names.Contains(a));
        if (keyExchangeAlgorithmName is null) {
            if (_logger.IsEnabled(LogLevel.Information)) {
                _logger.LogInformation("no key exchange algorithm");
                _logger.LogInformation("our   : {KexAlgorithms}", serverKexPayload.KexAlgorithms);
                _logger.LogInformation("their : {KexAlgorithms}", clientKexPayload.KexAlgorithms);
            }
            return null;
        }

        var keyExchangeAlgorithm = keyExchangeAlgorithms.FirstOrDefault(a => a.Name == keyExchangeAlgorithmName)!;
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("using keyExchangeAlgorithm {KeyExchangeAlgorithm}", keyExchangeAlgorithm);

        var macAlgorithmClientToServerName = clientKexPayload.MacAlgorithmsClientToServer.Names.FirstOrDefault(a => serverKexPayload.MacAlgorithmsClientToServer.Names.Contains(a));
        if (macAlgorithmClientToServerName is null) {
            if (_logger.IsEnabled(LogLevel.Information)) {
                _logger.LogInformation("no mac algorithm");
                _logger.LogInformation("our   : {MacAlgorithm}", serverKexPayload.MacAlgorithmsClientToServer);
                _logger.LogInformation("their : {MacAlgorithm}", clientKexPayload.MacAlgorithmsClientToServer);
            }
            return null;
        }


        var macAlgorithm = macAlgorithms.FirstOrDefault(a => a.Name == macAlgorithmClientToServerName)!;
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("using macAlgorithm {MacAlgorithm}", macAlgorithm);

        var publicKeyAlgorithmName = clientKexPayload.ServerHostKeyAlgorithms.Names.FirstOrDefault(a => serverKexPayload.ServerHostKeyAlgorithms.Names.Contains(a));
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
            null!,
            new(new NoMacAlgorithm(), 0, 0),
            keyExchangeAlgorithm,
            idenitificationStrings,
            publicKeyAlgorithm.GetHostKeyPair(),
            clientKexPayloadRaw.Inner.Payload,
            keyExchangePacket.Inner.Payload
        );
        if (await keyExchangeAlgorithm.ExchangeKeysAsync(sshState, cancellationToken)
            is not KeyExchangeResult(var secret, var exchangeHash))
            return null;
        var newKeysPacket = await new NewKeys().ToPacket(sshState, cancellationToken);
        await sshState.Stream.SshWritePacket(newKeysPacket, cancellationToken);
        sshState = sshState with {
            EncryptionKeys = GenerateInitialKeys(secret, exchangeHash, sshState.KeyExchangeAlgorithm),
            SessionId = exchangeHash,
            ExchangeHash = exchangeHash,
            Secret = secret,
            MacData = sshState.MacData with {
                MacAlgorithm = macAlgorithm
            }
        };
        return sshState;
    }

    private static KeyExchange GenerateKeyExchangePacket(IEnumerable<IKeyExchangeAlgorithm> keyExchangeAlgorithms, IEnumerable<IPublicKeyAlgorithm> publicKeyAlgorithms, IEnumerable<IEncryptionAlgorithm> encryptionAlgorithms, IEnumerable<IMacAlgorithm> macAlgorithms, IEnumerable<ICompressionAlgorithm> compressionAlgorithms, bool firstPacketFollows) {
        var cookie = RandomNumberGenerator.GetBytes(16);
        var encryptionAlgorithmsList = encryptionAlgorithms.ToNameList();
        var macAlgorithmsNameList = macAlgorithms.ToNameList();
        var compressionAlgorithmsNameList = compressionAlgorithms.ToNameList();
        return new(
            cookie,
            keyExchangeAlgorithms.ToNameList(),
            publicKeyAlgorithms.ToNameList(),
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
        return new(
            [HashWithParameter(secret, exchangeHash, (byte)'A', exchangeHash, keyExchangeAlgorithm)],
            [HashWithParameter(secret, exchangeHash, (byte)'B', exchangeHash, keyExchangeAlgorithm)],
            [HashWithParameter(secret, exchangeHash, (byte)'C', exchangeHash, keyExchangeAlgorithm)],
            [HashWithParameter(secret, exchangeHash, (byte)'D', exchangeHash, keyExchangeAlgorithm)],
            [HashWithParameter(secret, exchangeHash, (byte)'E', exchangeHash, keyExchangeAlgorithm)],
            [HashWithParameter(secret, exchangeHash, (byte)'F', exchangeHash, keyExchangeAlgorithm)]
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

public record IdenitificationStrings(string Server, string Client);
public record SshState(
    byte[] SessionId,
    Stream Stream,
    BigInteger Secret,
    byte[] ExchangeHash,
    EncryptionKeys EncryptionKeys,
    MacData MacData,
    IKeyExchangeAlgorithm KeyExchangeAlgorithm,
    IdenitificationStrings IdenitificationStrings,
    IHostKeyPair HostKeyPair,
    byte[] ClientKexInit, byte[] ServerKexInit) { }

public record MacData(IMacAlgorithm MacAlgorithm, uint MacSequenceClient, uint MacSequenceServer);
public record EncryptionKeys(
    List<byte[]> IVsClienttoServer,
    List<byte[]> IVsServerToClient,
    List<byte[]> EncryptionKeysClienttoServer,
    List<byte[]> EncryptionKeysServerToClient,
    List<byte[]> IntegrityKeysClienttoServer,
    List<byte[]> IntegrityKeysServerToClient
);
