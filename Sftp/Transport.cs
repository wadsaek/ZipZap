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
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;


using ZipZap.Sftp.Ssh;
using ZipZap.Sftp.Ssh.Algorithms;
using ZipZap.Sftp.Ssh.Numbers;
using ZipZap.Sftp.Ssh.Services;

namespace ZipZap.Sftp;

internal class Transport {

    private readonly ILogger<Transport> _logger;
    private readonly ISshConnectionFactory _connectionFactory;
    private readonly KeyExchangeProcess _kexProcess;
    private readonly IAuthServiceFactory _loginFactory;
    private readonly IProvider<IPublicKeyAlgorithm> _publicKeyAlgs;

    public Transport(
        ILogger<Transport> logger,
        ISshConnectionFactory handler,
        IAuthServiceFactory loginFactory,
        KeyExchangeProcess kexProcess,
        IProvider<IPublicKeyAlgorithm> publicKeyAlgs) {
        _logger = logger;
        _connectionFactory = handler;
        _kexProcess = kexProcess;
        _loginFactory = loginFactory;
        _publicKeyAlgs = publicKeyAlgs;
    }

    private class TransportClient : ITransportClient {
        int _untilNextIgnore = RandomNumberGenerator.GetInt32(5);
        // NOTE: Although the setter is public, the class itself is not.
        // The SshState property is also not inherited from `ITransportClient`.
        // This means, that i can only access the property from inside
        // `Transport` in order to change. This makes it trivial to update
        // the client info when doing a rekey.
        public SshState SshState { get; set; }

        public byte[] SessionId => SshState.SessionId;
        public uint LastPacketId => SshState.Decryptor.MacSequential - 1;

        internal TransportClient(SshState sshState) {
            SshState = sshState;
        }


        public async Task SendPacket<T>(T packet, CancellationToken cancellationToken)
        where T : IServerPayload {
            if (_untilNextIgnore-- == 0) {
                _untilNextIgnore = RandomNumberGenerator.GetInt32(5);
                await SshState.Encryptor.SendPacket(Ignore.Random(), cancellationToken);
            }
            await SshState.Encryptor.SendPacket(packet, cancellationToken);
        }

        public void End() {
            throw new NotImplementedException();
        }

        public async Task SendUnimplemented(CancellationToken cancellationToken) {
            await ReplyUnimplemented(SshState, cancellationToken);
        }
    }
    public async Task Handle(Stream stream, IdenitificationStrings idenitificationStrings, CancellationToken cancellationToken) {

        var decryptor = new NoEncryptionAlgorithm().GetDecryptor(
            stream, [], [],
            new NoMacAlgorithm().CreateValidator(0, []));
        var encryptor = new NoEncryptionAlgorithm().GetEncryptor(
            stream, [], [],
            new NoMacAlgorithm().CreateGenerator(0, []));

        var input = new KeyExchangeInput(
            stream,
            new PacketReaderTransportPassThrough(this, decryptor, encryptor),
            SessionId: null,
            idenitificationStrings
        );

        var state = await _kexProcess.MakeKeyExchange(input, null, cancellationToken);

        if (state is null) return;
        if (state.SupportsExtensions) {
            Extension[] extensions = [
                new Extension.ServerSigAlgs(new(_publicKeyAlgs.Items.SelectMany(i=>i.SupportedSignatureAlgs).ToArray())),
                new Extension.NoFlowControl(true)
            ];
            await state.Encryptor.SendPacket(new ExtInfo(extensions), cancellationToken);
        }
        var transportClient = new TransportClient(state);
        ISshConnection? connectionService;
        IAuthService? loginService = null;

        ISshService? currentService = null;
        bool firstPacketRecieved = false;
        bool noFlowControl = false;

        while (await state.Decryptor.ReadPacket(cancellationToken) is Packet packet) {
            var payload = packet.Payload;
            if (payload.Length == 0) break;
            var msg = (Message)payload[0];
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("Recieved packet {Msg}", msg);
            switch (msg.GetMessageCategory()) {
                case MessageCategory.ExtInfo: {
                        if (firstPacketRecieved) {
                            var disconnect = new Disconnect(
                                DisconnectCode.ProtocolError,
                                "Recieved extensions after first packet"
                            );
                            await state.Encryptor.SendPacket(disconnect, cancellationToken);
                            return;
                        }
                        if (!ExtInfo.TryParse(payload, out var extInfo)) {
                            var disconnect = new Disconnect(
                                DisconnectCode.ProtocolError,
                                "Unable to parse ExtInfo"
                            );
                            await state.Encryptor.SendPacket(disconnect, cancellationToken);
                            return;
                        }
                        var noFlowExt = extInfo.Extensions.FirstOrDefault(e => e is Extension.NoFlowControl);
                        // we prefer it, so if the client supports it we can enable it
                        noFlowControl = noFlowExt is not null;
                        break;
                    }
                case MessageCategory.Invalid:
                    await ReplyUnimplemented(state, cancellationToken);
                    break;
                case MessageCategory.TransportGeneric:
                    if (await HandleGenericTransportMsg(packet, encryptor, cancellationToken)) return;
                    break;
                case MessageCategory.KexInit:
                    var newInput = input with {
                        Reader = new PacketReaderTransportPassThrough(this, state.Decryptor, state.Encryptor),
                        SessionId = state.SessionId
                    };
                    var maybeState = await RedoKeyExchange(packet, newInput, cancellationToken);
                    if (maybeState is null) return;
                    state = maybeState;
                    transportClient.SshState = state;
                    break;
                case MessageCategory.KeyExchange or MessageCategory.KeyExchangeAlgSpecific:
                    if (_logger.IsEnabled(LogLevel.Critical))
                        _logger.LogCritical("recieved a key exchange message {Msg} without outside of a key exchange", msg);
                    await state.Encryptor.SendPacket(
                        new Disconnect(DisconnectCode.ProtocolError, "Unexpected kex packet"),
                        cancellationToken
                    );
                    return;
                case MessageCategory.Service:
                    if (msg == Message.ServiceAccept) {
                        await ReplyUnimplemented(state, cancellationToken);
                        break;
                    }
                    if (!ServiceRequest.TryParse(payload, out var request)) {

                        await ReplyUnimplemented(state, cancellationToken);
                        break;
                    }
                    if (request.ServiceName == IAuthService.ServiceName) {
                        currentService = loginService = _loginFactory.Create(transportClient);
                        await state.Encryptor.SendPacket(new ServiceAccept(request.ServiceName), cancellationToken);
                        break;
                    } else if (request.ServiceName == ISshConnection.ServiceName) {
                        if (loginService is null || !loginService.TryGetRequestHandler(out var handler)) {
                            var disconnect = new Disconnect(
                                DisconnectCode.ServiceNotAvailable,
                                "ssh-connection was requested before ssh-userauth");
                            await state.Encryptor.SendPacket(disconnect, cancellationToken);
                            return;
                        }
                        connectionService = _connectionFactory.Create(transportClient, handler);
                        currentService = connectionService;
                        await state.Encryptor.SendPacket(new ServiceAccept(request.ServiceName), cancellationToken);
                        break;
                    } else {
                        var disconnect = new Disconnect(DisconnectCode.ServiceNotAvailable, "Service name not recognized");
                        await state.Encryptor.SendPacket(disconnect, cancellationToken);
                        if (_logger.IsEnabled(LogLevel.Information))
                            _logger.LogInformation("An unrecognized service \"{ServiceName}\" was requested", request.ServiceName);
                        return;
                    }
                default:
                    if (currentService is null) {
                        var disconnect = new Disconnect(
                            DisconnectCode.ProtocolError,
                            "Recieved a service message without negotiating a server beforehand"
                        );
                        await state.Encryptor.SendPacket(disconnect, cancellationToken);
                        return;
                    }
                    await currentService.SendPacket(packet, cancellationToken);
                    break;
            }
            firstPacketRecieved = true;
        }

        var finalDisconnect = new Disconnect(DisconnectCode.ProtocolError, "Unable to parse packet");
        await state.Encryptor.SendPacket(finalDisconnect, cancellationToken);
        return;
    }

    private async Task<SshState?> RedoKeyExchange(Packet? packet, KeyExchangeInput input, CancellationToken cancellationToken) {
        return await _kexProcess.MakeKeyExchange(input, packet, cancellationToken);
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

        public Task<(Packet, T)?> ReadUntilPacket<T>(CancellationToken cancellationToken)
        where T : IClientPayload<T>
        => _transport.ReadUntilPacket<T>(_decryptor, _encryptor, cancellationToken);

        public async Task SendPacket<T>(T packet, CancellationToken cancellationToken)
        where T : IServerPayload {
            if (_untilNextIgnore-- == 0) {
                _untilNextIgnore = RandomNumberGenerator.GetInt32(5);
                await _encryptor.SendPacket(Ignore.Random(), cancellationToken);
            }
            await _encryptor.SendPacket(packet, cancellationToken);
        }
    }

    private async Task<(Packet, T)?> ReadUntilPacket<T>(IDecryptor decryptor, IEncryptor encryptor, CancellationToken cancellationToken)
    where T : IClientPayload<T> {

        Packet? packet;
        T? parsed;
        bool success;

        do {
            packet = await decryptor.ReadPacket(cancellationToken);
            if (packet is null) return null;
            success = T.TryParse(packet.Payload, out parsed);
            if (!success) {
                if (await HandleGenericTransportMsg(packet, encryptor, cancellationToken)) return null;
            }
        }
        while (!success);
        return (packet, parsed!);
    }

    ///<returns>A bool indicating whether the caller should stop execution</returns>
    private async Task<bool> HandleGenericTransportMsg(Packet packet, IEncryptor encryptor, CancellationToken cancellationToken) {

        switch ((Message)packet.Payload[0]) {
            case Message.Ignore: return false;
            case Message.Disconnect: {
                    if (!Disconnect.TryParse(packet.Payload, out var disconnect)) return true;
                    if (_logger.IsEnabled(LogLevel.Information))
                        _logger.LogInformation("Got a disconnect {Code} with description {Description}", disconnect.ReasonCode, disconnect.Description);
                    return true;
                }
            case Message.Debug: {
                    if (!Debug.TryParse(packet.Payload, out var debug)) {
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
                    if (!Unimplemented.TryParse(packet.Payload, out _)) {
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
    public Task<(Packet, T)?> ReadUntilPacket<T>(CancellationToken cancellationToken)
    where T : IClientPayload<T>;

    public Task SendPacket<T>(T packet, CancellationToken cancellationToken)
    where T : IServerPayload;

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
    public Task SendUnimplemented(CancellationToken cancellationToken);
    Task SendPacket<T>(T Packet, CancellationToken cancellationToken) where T : IServerPayload;
    void End();
}
