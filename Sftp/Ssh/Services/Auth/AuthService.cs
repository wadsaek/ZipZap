// AuthService.cs - Part of the ZipZap project for storing files online
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
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using ZipZap.LangExt.Helpers;
using ZipZap.Sftp.Ssh.Algorithms;
using ZipZap.Sftp.Ssh.Numbers;
using ZipZap.Sftp.Ssh.Services.Auth.Packets;
using ZipZap.Sftp.Ssh.Services.Connection;

using static ZipZap.LangExt.Helpers.ResultConstructor;

namespace ZipZap.Sftp.Ssh.Services;

internal class AuthService : SshService, IAuthService {
    private readonly ITransportClient _transport;
    private readonly ISftpLoginHandler _handler;
    private readonly IEnumerable<IPublicKeyAlgorithm> _publicKeys;
    private readonly IEnumerable<IServerHostKeyAlgorithm> _hostKeys;
    private readonly ISshConnectionFactory _connectionFactory;
    private readonly ILogger<AuthService> _logger;
    private ISshService? _aggregate = null;

    public AuthService(
        ITransportClient transport,
        ISftpLoginHandler handler,
        IEnumerable<IPublicKeyAlgorithm> publicKeys,
        IEnumerable<IServerHostKeyAlgorithm> hostKeys,
        ISshConnectionFactory connectionFactory,
        ILogger<AuthService> logger
    ) : base(logger) {
        _transport = transport;
        _handler = handler;
        _publicKeys = publicKeys;
        _hostKeys = hostKeys;
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public override string ServiceName => IAuthService.ServiceName;

    ISftpRequestHandler? _returnedHandler;
    public bool TryGetRequestHandler([NotNullWhen(true)] out ISftpRequestHandler? sftpRequestHandler) {
        sftpRequestHandler = _returnedHandler;
        return sftpRequestHandler is not null;
    }

    protected override Task ReturnPacket(IServerPayload packet, CancellationToken cancellationToken) {
        return _transport.SendPacket(packet, cancellationToken);
    }

    protected override async Task End(Disconnect disconnect, CancellationToken cancellationToken) {
        await ReturnPacket(disconnect, cancellationToken);
        _transport.End();
    }

    protected override Task HandlePacket(Payload packet, CancellationToken cancellationToken) {
        if (IsPassThrough())
            return _aggregate.Send(packet, cancellationToken);
        return HandleAuthPacket(packet, cancellationToken);
    }

    [MemberNotNullWhen(true, nameof(_aggregate))]
    private bool IsPassThrough() {
        return _aggregate is not null;
    }

    protected async Task HandleAuthPacket(Payload packet, CancellationToken cancellationToken) {
        if (!UserauthRequest.TryParse(packet, out var request)) {
            await _transport.SendUnimplemented(cancellationToken);
            return;
        }
        if (request.ServiceName != ISshConnectionFactory.ServiceName) {
            var disconnect = new Disconnect(DisconnectCode.ServiceNotAvailable, "requested service not availible");
            await End(disconnect, cancellationToken);
            return;
        }
        var result = request switch {
            UserauthRequest.Password passwordRequest => await HandlePasswordRequest(passwordRequest, cancellationToken),
            UserauthRequest.PublicKey publickeyRequest => await HandlePublicKey(publickeyRequest, cancellationToken),
            _ or UserauthRequest.Unrecognized or UserauthRequest.None => Err<ISftpRequestHandler, LoginError>(new LoginError.EmptyCredentials()),
        };
        await result
        .Select(handler => HandleSuccess(request, handler, cancellationToken))
        .UnwrapOrElse(err => err switch {
            LoginError.SignatureNotProvided(var publicKey) => SendPKOkay(publicKey, cancellationToken),
            _ => HandleError(err, cancellationToken),
        });
    }

    private async Task HandleError(LoginError err, CancellationToken cancellationToken) {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Unable to login. Error {Err}", err);
        var reply = new UserauthFailure(
            new NameList([
                new NameList.GlobalName("publickey"),
                new NameList.GlobalName("password")
            ]),
            false);
        await ReturnPacket(reply, cancellationToken);
    }

    private async Task SendPKOkay(IPublicKey publicKey, CancellationToken cancellationToken) {
        var reply = new PkOk(publicKey.AlgorithmName, publicKey.ToByteString());
        await ReturnPacket(reply, cancellationToken);
    }

    private async Task HandleSuccess(UserauthRequest request, ISftpRequestHandler handler, CancellationToken cancellationToken) {
        _returnedHandler = handler;
        var reply = new UserauthSuccess();
        if (request.ServiceName == ISshConnectionFactory.ServiceName) {
            _aggregate = _connectionFactory.Create(_transport, handler);
            await ReturnPacket(reply, cancellationToken);
            return;
        }
    }

    private async Task<Result<ISftpRequestHandler, LoginError>> HandlePublicKey(UserauthRequest.PublicKey publickeyRequest, CancellationToken cancellationToken) {
        var publicKeyAlg = _publicKeys.FirstOrDefault(i => i.SupportedSignatureAlgs.Select(n => n.ToString()).Contains(publickeyRequest.AlgName));
        if (publicKeyAlg is null
            || !publicKeyAlg.TryParse(publickeyRequest.PublicKeyBytes, out var key))
            return Err<ISftpRequestHandler, LoginError>(new LoginError.WrongCredentials());

        foreach (var hostKeyAlg in _hostKeys) {
            var pair = hostKeyAlg.GetHostKeyPair();
            var result = await _handler.TryLoginPublicKey(
                publickeyRequest.Username, key, pair, cancellationToken
            );
            if (result is Err<ISftpRequestHandler, LoginError>(LoginError.HostPublicKeyNotAuthorized)) continue;
            return result;
        }
        // this only fires off if there are no host keys
        System.Diagnostics.Debug.Print("No host keys");
        return Err<ISftpRequestHandler, LoginError>(new LoginError.Other());
    }

    private async Task<Result<ISftpRequestHandler, LoginError>> HandlePasswordRequest(UserauthRequest.Password passwordRequest, CancellationToken cancellationToken) {
        var result = await _handler.TryLoginPassword(
            passwordRequest.Username, passwordRequest.CurrentPassword,
            cancellationToken
        );
        return result;
    }

}
