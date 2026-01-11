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

    public SftpService(ISftpRequestHandler handler, ISftpConfiguration configuration, ILogger<SftpService> logger) {
        _handler = handler;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken) {
        var listener = new TcpListener(IPAddress.Any, _configuration.Port);
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("listening on port {port}", _configuration.Port);
        var tasks = new List<Task>();
        listener.Start();
        while (!cancellationToken.IsCancellationRequested) {
            using var socket = await listener.AcceptSocketAsync(cancellationToken);
            tasks.Add(HandleSocket(socket, cancellationToken));
            var removed = await tasks.RemoveCompleted();
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("{Count} tasks in queue\n{Removed} tasks removed", tasks.Count, removed);
        }
    }

    private async Task HandleSocket(Socket socket, CancellationToken cancellationToken) {

        await using var stream = new NetworkStream(socket);
        var header = Encoding.ASCII.GetBytes($"SSH-2.0-{_configuration.ServerName}_0.1.0\r\n");
        await stream.WriteAsync(header, cancellationToken);

        var clientHeader = new List<byte>();
        while (clientHeader.Count < 2 || (clientHeader[^2] != (byte)'\r' && clientHeader[^1] != (byte)'\n'))
            clientHeader.Add((byte)stream.ReadByte());
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("handling client {client-id}", Encoding.ASCII.GetString(clientHeader.ToArray()));

        var firstPacket = await stream.SshTryReadPacket(0, cancellationToken);
        if (firstPacket is null) return;
        var kexPayload = await KeyExchange.TryFromPayload(firstPacket.Value.Payload, cancellationToken);
        if (kexPayload is null) return;
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("got keyExchange packet {packet}", kexPayload);
    }
}

public record IdenitificationStrings(string Server, string Client);
public record SshState(Stream Stream, BigInteger Secret, IMacAlgorithm MacAlgorithm, IdenitificationStrings IdenitificationStrings, HostKeyPair HostKeyPair, byte[] ClientKexInit, byte[] ServerKexInit) { }
