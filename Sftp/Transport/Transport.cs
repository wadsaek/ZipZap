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
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;


using ZipZap.Sftp.Ssh;
using ZipZap.Sftp.Ssh.Algorithms;
using ZipZap.Sftp.Ssh.Numbers;
using ZipZap.Sftp.Ssh.Services;
using ZipZap.Sftp.Ssh.Services.Connection;

namespace ZipZap.Sftp;

internal class Transport {

    private readonly ILogger<Transport> _logger;
    private readonly ISshConnectionFactory _connectionFactory;
    private readonly KeyExchangeProcess _kexProcess;
    private readonly IAuthServiceFactory _loginFactory;
    private readonly IPublicKeyAlgorithm[] _publicKeyAlgs;

    public Transport(
        ILogger<Transport> logger,
        ISshConnectionFactory handler,
        IAuthServiceFactory loginFactory,
        KeyExchangeProcess kexProcess,
        IEnumerable<IPublicKeyAlgorithm> publicKeyAlgs) {
        _logger = logger;
        _connectionFactory = handler;
        _kexProcess = kexProcess;
        _loginFactory = loginFactory;
        _publicKeyAlgs = publicKeyAlgs.ToArray();
    }

    private class TransportClient : ITransportClient {
        int _untilNextIgnore = RandomNumberGenerator.GetInt32(5);
        // NOTE: Although the setter is public, the class itself is not.
        // The SshState property is also not inherited from `ITransportClient`.
        // This means, that i can only access the property from inside
        // `Transport` in order to change. This makes it trivial to update
        // the client info when doing a rekey.
        public SshState SshState { get; set; }
        public CancellationTokenSource TokenSource { get; }
        private readonly ILogger<Transport> _logger;

        public byte[] SessionId => SshState.SessionId;
        public uint LastPacketId => SshState.Decryptor.MacSequential - 1;

        public bool NoFlowControlEnabled { get; set; }

        internal TransportClient(SshState sshState, CancellationTokenSource tokenSource, bool noFlowControlEnabled, ILogger<Transport> logger) {
            SshState = sshState;
            TokenSource = tokenSource;
            NoFlowControlEnabled = noFlowControlEnabled;
            _logger = logger;
        }


        public async Task SendPacket(IServerPayload packet, CancellationToken cancellationToken) {
            try {
                if (_untilNextIgnore-- == 0) {
                    _untilNextIgnore = RandomNumberGenerator.GetInt32(5);
                    await SshState.Encryptor.SendPacket(Ignore.Random(), cancellationToken);
                }
                await SshState.Encryptor.SendPacket(packet, cancellationToken);
            } catch (IOException exception) {
                if (exception.GetBaseException() is SocketException { SocketErrorCode: SocketError.Shutdown }) {
                    HandleDisconnect(cancellationToken);
                }
                throw;
            } catch (ObjectDisposedException) {
                HandleDisconnect(cancellationToken);
            }

        }

        private void HandleDisconnect(CancellationToken cancellationToken) {
            _logger.LogInformation("Client disconnected");
            End();
            cancellationToken.ThrowIfCancellationRequested();
            return;
        }

        public void End() {
            TokenSource.Cancel();
        }

        public async Task SendUnimplemented(CancellationToken cancellationToken) {
            await ReplyUnimplemented(SshState, cancellationToken);
        }
    }

    private class HandleState {
        public required SshState State { get; set; }
        public required ISshConnection? ConnectionService { get; set; }
        public required IAuthService? AuthService { get; set; }
        public required ISshService? CurrentService { get; set; }
        public required bool FirstPacketRecieved { get; set; }
        public required KeyExchangeInput Input { get; set; }
        public required TransportClient TransportClient { get; set; }
    }

    public async Task HandleStream(Stream stream, IdenitificationStrings idenitificationStrings, CancellationToken cancellationToken) {

        using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cancellationToken = linkedTokenSource.Token;

        var (decryptor, encryptor) = GenerateDefaultEncryptors(stream);

        var input = new KeyExchangeInput(
            stream,
            new PacketReaderTransportPassThrough(this, decryptor, encryptor),
            SessionId: null,
            idenitificationStrings
        );

        var state = await _kexProcess.MakeKeyExchange(input, null, cancellationToken);

        if (state is null) return;
        if (state.SupportsExtensions)
            await SendExtensions(state, cancellationToken);

        var transportClient = new TransportClient(state, linkedTokenSource, false, _logger);

        var handlerState = new HandleState {
            State = state,
            CurrentService = null,
            AuthService = null,
            ConnectionService = null,
            FirstPacketRecieved = false,
            TransportClient = transportClient,
            Input = input
        };

        while (await handlerState.State.Decryptor.ReadPacket(cancellationToken) is Payload packet) {
            cancellationToken.ThrowIfCancellationRequested();
            bool shouldContinue = await HandlePacket(packet, handlerState, cancellationToken);
            if (!shouldContinue) {
                return;
            }
            handlerState.FirstPacketRecieved = true;
        }

        var finalDisconnect = new Disconnect(DisconnectCode.ProtocolError, "Unable to parse packet");
        await state.Encryptor.SendPacket(finalDisconnect, cancellationToken);
        return;
    }

    private async Task SendExtensions(SshState state, CancellationToken cancellationToken) {
        Extension[] extensions = [
            new Extension.ServerSigAlgs(new(_publicKeyAlgs.SelectMany(i=>i.SupportedSignatureAlgs).ToArray())),
                new Extension.NoFlowControl(true)
        ];
        await state.Encryptor.SendPacket(new ExtInfo(extensions), cancellationToken);
    }

    private static (IDecryptor decryptor, IEncryptor encryptor) GenerateDefaultEncryptors(Stream stream) {
        var decryptor = new NoEncryptionAlgorithm().GetDecryptor(
            stream, [], [],
            new NoMacAlgorithm().CreateValidator(0, []));
        var encryptor = new NoEncryptionAlgorithm().GetEncryptor(
            stream, [], [],
            new NoMacAlgorithm().CreateGenerator(0, []));
        return (decryptor, encryptor);
    }

    /// <returns>A boolean indicating whether caller should continue</returns>
    private async Task<bool> HandlePacket(Payload packet, HandleState handleState, CancellationToken cancellationToken) {
        if (packet.Length == 0) return false;
        var msg = (Message)packet[0];
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Recieved packet {Msg}", msg);
        switch (msg.GetMessageCategory()) {
            case MessageCategory.ExtInfo: {
                    if (handleState.FirstPacketRecieved) {
                        var disconnect = new Disconnect(
                            DisconnectCode.ProtocolError,
                            "Recieved extensions after first packet"
                        );
                        await handleState.State.Encryptor.SendPacket(disconnect, cancellationToken);
                        return false;
                    }
                    if (!ExtInfo.TryParse(packet, out var extInfo)) {
                        var disconnect = new Disconnect(
                            DisconnectCode.ProtocolError,
                            "Unable to parse ExtInfo"
                        );
                        await handleState.State.Encryptor.SendPacket(disconnect, cancellationToken);
                        return false;
                    }
                    if (_logger.IsEnabled(LogLevel.Debug))
                        _logger.LogDebug("recieved extensions: {ExtInfo}", extInfo);
                    var noFlowExt = extInfo.Extensions.FirstOrDefault(e => e is Extension.NoFlowControl);
                    // we prefer it, so if the client supports it we can enable it
                    handleState.TransportClient.NoFlowControlEnabled = noFlowExt is not null;
                    break;
                }
            case MessageCategory.Invalid: {
                    await ReplyUnimplemented(handleState.State, cancellationToken);
                    break;
                }
            case MessageCategory.TransportGeneric: {
                    if (await HandleGenericTransportMsg(packet, handleState.State.Encryptor, cancellationToken)) return false;
                    break;
                }
            case MessageCategory.KexInit: {
                    var newInput = handleState.Input with {
                        Reader = new PacketReaderTransportPassThrough(this, handleState.State.Decryptor, handleState.State.Encryptor),
                        SessionId = handleState.State.SessionId
                    };
                    var maybeState = await RedoKeyExchange(packet, newInput, cancellationToken);
                    if (maybeState is null) return false;
                    handleState.State = maybeState;
                    handleState.TransportClient.SshState = handleState.State;
                    break;
                }
            case MessageCategory.KeyExchange or MessageCategory.KeyExchangeAlgSpecific: {
                    if (_logger.IsEnabled(LogLevel.Critical))
                        _logger.LogCritical("recieved a key exchange message {Msg} without outside of a key exchange", msg);
                    await handleState.State.Encryptor.SendPacket(
                            new Disconnect(DisconnectCode.ProtocolError, "Unexpected kex packet"),
                            cancellationToken
                            );
                    return false;
                }
            case MessageCategory.Service: {
                    if (msg == Message.ServiceAccept) {
                        await ReplyUnimplemented(handleState.State, cancellationToken);
                        break;
                    }
                    if (!ServiceRequest.TryParse(packet, out var request)) {

                        await ReplyUnimplemented(handleState.State, cancellationToken);
                        break;
                    }
                    if (request.ServiceName == IAuthService.ServiceName) {
                        handleState.CurrentService = handleState.AuthService = _loginFactory.Create(handleState.TransportClient);
                        await handleState.State.Encryptor.SendPacket(new ServiceAccept(request.ServiceName), cancellationToken);
                        break;
                    } else if (request.ServiceName == ISshConnection.ServiceName) {
                        if (handleState.AuthService is null || !handleState.AuthService.TryGetRequestHandler(out var handler)) {
                            var disconnect = new Disconnect(
                                    DisconnectCode.ServiceNotAvailable,
                                    "ssh-connection was requested before ssh-userauth");
                            await handleState.State.Encryptor.SendPacket(disconnect, cancellationToken);
                            return false;
                        }
                        handleState.ConnectionService = _connectionFactory.Create(handleState.TransportClient, handler);
                        handleState.CurrentService = handleState.ConnectionService;
                        await handleState.State.Encryptor.SendPacket(new ServiceAccept(request.ServiceName), cancellationToken);
                        break;
                    } else {
                        var disconnect = new Disconnect(DisconnectCode.ServiceNotAvailable, "Service name not recognized");
                        await handleState.State.Encryptor.SendPacket(disconnect, cancellationToken);
                        if (_logger.IsEnabled(LogLevel.Information))
                            _logger.LogInformation("An unrecognized service \"{ServiceName}\" was requested", request.ServiceName);
                        return false;
                    }
                }
            default: {
                    if (handleState.CurrentService is null) {
                        var disconnect = new Disconnect(
                                DisconnectCode.ProtocolError,
                                "Recieved a service message without negotiating a server beforehand"
                                );
                        await handleState.State.Encryptor.SendPacket(disconnect, cancellationToken);
                        return false;
                    }
                    await handleState.CurrentService.Send(packet, cancellationToken);
                    break;
                }
        }
        return true;
    }

    private Task<SshState?> RedoKeyExchange(Payload? packet, KeyExchangeInput input, CancellationToken cancellationToken) {
        return _kexProcess.MakeKeyExchange(input, packet, cancellationToken);
    }


    private class PacketReaderTransportPassThrough : ITransPacketReader {
        int _untilNextIgnore = RandomNumberGenerator.GetInt32(5);
        internal PacketReaderTransportPassThrough(Transport transport, IDecryptor decryptor, IEncryptor encryptor) {
            _transport = transport;
            _decryptor = decryptor;
            _encryptor = encryptor;
        }

        private readonly Transport _transport;
        private readonly IDecryptor _decryptor;
        private readonly IEncryptor _encryptor;

        public uint SequentialCtS => _decryptor.MacSequential;

        public uint SequentialStC => _encryptor.MacSequential;

        public Task<(Payload, T)?> ReadUntilPacket<T>(CancellationToken cancellationToken)
        where T : IClientPayload<T>
        => _transport.ReadUntilPacket<T>(_decryptor, _encryptor, cancellationToken);

        public async Task SendPacket(IServerPayload packet, CancellationToken cancellationToken) {
            if (_untilNextIgnore-- == 0) {
                _untilNextIgnore = RandomNumberGenerator.GetInt32(5);
                await _encryptor.SendPacket(Ignore.Random(), cancellationToken);
            }
            await _encryptor.SendPacket(packet, cancellationToken);
        }
    }

    private async Task<(Payload, T)?> ReadUntilPacket<T>(IDecryptor decryptor, IEncryptor encryptor, CancellationToken cancellationToken)
    where T : IClientPayload<T> {

        Payload? packet;
        T? parsed;
        bool success;

        do {
            packet = await decryptor.ReadPacket(cancellationToken);
            if (packet is null) return null;
            success = T.TryParse(packet, out parsed);
            if (!success) {
                if (await HandleGenericTransportMsg(packet, encryptor, cancellationToken)) return null;
            }
        }
        while (!success);
        return (packet, parsed!);
    }

    ///<returns>A bool indicating whether the caller should stop execution</returns>
    private async Task<bool> HandleGenericTransportMsg(Payload packet, IEncryptor encryptor, CancellationToken cancellationToken) {

        switch ((Message)packet[0]) {
            case Message.Ignore: return false;
            case Message.Disconnect: {
                    if (!Disconnect.TryParse(packet, out var disconnect)) return true;
                    if (_logger.IsEnabled(LogLevel.Information))
                        _logger.LogInformation("Got a disconnect {Code} with description {Description}", disconnect.ReasonCode, disconnect.Description);
                    return true;
                }
            case Message.Debug: {
                    if (!Debug.TryParse(packet, out var debug)) {
                        var disconnect = new Disconnect(
                            DisconnectCode.ProtocolError,
                            $"Can't parse a {nameof(Debug)} packet"
                        );
                        await encryptor.SendPacket(disconnect, cancellationToken);
                        return true;
                    }
                    if (debug.Display && _logger.IsEnabled(LogLevel.Debug))
                        _logger.LogDebug("Got a debug message with description {Description}", debug.Description);

                    return false;
                }
            case Message.Unimplemented: {
                    if (!Unimplemented.TryParse(packet, out _)) {
                        var disconnect = new Disconnect(
                            DisconnectCode.ProtocolError,
                            "Can't read the message type of an unimplemented message"
                        );
                        await encryptor.SendPacket(disconnect, cancellationToken);
                    }
                    return false;
                }
            default: {
                    return false;
                }
        }
    }
    private static async Task ReplyUnimplemented(SshState state, CancellationToken cancellationToken) {
        var unimplemented = new Unimplemented(state.Decryptor.MacSequential - 1);
        await state.Encryptor.SendPacket(unimplemented, cancellationToken);

    }
}


public interface ITransPacketReader {
    public Task<(Payload, T)?> ReadUntilPacket<T>(CancellationToken cancellationToken)
    where T : IClientPayload<T>;

    public Task SendPacket(IServerPayload packet, CancellationToken cancellationToken);

    public uint SequentialCtS { get; }
    public uint SequentialStC { get; }
}


internal class AlgorithmCollection {
    public AlgorithmCollection(
        IKeyExchangeAlgorithm keyExchangeAlgorithm,
        IMacAlgorithm macAlgorithmCtS,
        IEncryptionAlgorithm encAlgorithmCtS,
        IMacAlgorithm macAlgorithmStC,
        IEncryptionAlgorithm encAlgorithmStC,
        IServerHostKeyAlgorithm publicKeyAlgorithm
    ) {
        KeyExchangeAlgorithm = keyExchangeAlgorithm;
        MacAlgorithmCtS = macAlgorithmCtS;
        EncAlgorithmCtS = encAlgorithmCtS;
        MacAlgorithmStC = macAlgorithmStC;
        EncAlgorithmStC = encAlgorithmStC;
        PublicKeyAlgorithm = publicKeyAlgorithm;
    }

    public IKeyExchangeAlgorithm KeyExchangeAlgorithm { get; }
    public IMacAlgorithm MacAlgorithmCtS { get; }
    public IEncryptionAlgorithm EncAlgorithmCtS { get; }
    public IMacAlgorithm MacAlgorithmStC { get; }
    public IEncryptionAlgorithm EncAlgorithmStC { get; }
    public IServerHostKeyAlgorithm PublicKeyAlgorithm { get; }
}

internal record EncryptionKeys(
    byte[] IvCtS,
    byte[] IvStC,
    byte[] EncryptionCtS,
    byte[] EncryptionStC,
    byte[] IntegrityCtS,
    byte[] IntegrityStC);

public record IdenitificationStrings(string Server, string Client);

public interface ITransportClient {
    public byte[] SessionId { get; }
    public bool NoFlowControlEnabled { get; }
    public Task SendUnimplemented(CancellationToken cancellationToken);
    Task SendPacket(IServerPayload Packet, CancellationToken cancellationToken);
    void End();
}
