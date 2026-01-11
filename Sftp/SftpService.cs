using System;
using System.Collections;
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
        if (_logger.IsEnabled(LogLevel.Information)) {
            _logger.LogInformation("got keyExchange packet     {Packet}", clientKexPayload);
            _logger.LogInformation("sending keyExchange packet {Packet}", serverKexPayload);
        }
        var keyExchangePacket = await serverKexPayload.ToPacket(cancellationToken);
        var bytes = await keyExchangePacket.ToByteString(cancellationToken);
        await File.WriteAllBytesAsync("packet", bytes, cancellationToken);
        var keyExchangePacketMac = new Packet(keyExchangePacket, []);
        await stream.SshWritePacket(keyExchangePacketMac, cancellationToken);

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
            stream,
            0,
            macAlgorithm,
            idenitificationStrings,
            publicKeyAlgorithm.GetHostKeyPair(),
            clientKexPayloadRaw.Inner.Payload,
            keyExchangePacket.Payload
        );
        if (await keyExchangeAlgorithm.ExchangeKeysAsync(sshState, cancellationToken) is not BigInteger secret) return null;
        return sshState with { Secret = secret };
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

        } catch (OperationCanceledException e) {
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("Operation was cancelled, {e}", e);
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
}

public record IdenitificationStrings(string Server, string Client);
public record SshState(Stream Stream, BigInteger Secret, IMacAlgorithm MacAlgorithm, IdenitificationStrings IdenitificationStrings, HostKeyPair HostKeyPair, byte[] ClientKexInit, byte[] ServerKexInit) { }
