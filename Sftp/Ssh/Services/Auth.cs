// Auth.cs - Part of the ZipZap project for storing files online
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

using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

using ZipZap.LangExt.Helpers;
using static ZipZap.LangExt.Helpers.ResultConstructor;
using ZipZap.Sftp.Ssh.Auth;
using ZipZap.Sftp.Ssh.Algorithms;
using System.ComponentModel;
using System.Linq;

namespace ZipZap.Sftp.Ssh.Services;

internal interface IAuthServiceFactory {
    IAuthService Create(ITransportClient transport);
}
internal class AuthServiceFactory : IAuthServiceFactory {
    private readonly ISftpRequestHandlerFactory _factory;
    private readonly IProvider<IPublicKeyAlgorithm> _publicKeys;
    private readonly IProvider<IServerHostKeyAlgorithm> _hostKeys;

    public AuthServiceFactory(ISftpRequestHandlerFactory factory, IProvider<IPublicKeyAlgorithm> publicKeys, IProvider<IServerHostKeyAlgorithm> hostKeys) {
        _factory = factory;
        _publicKeys = publicKeys;
        _hostKeys = hostKeys;
    }

    public IAuthService Create(ITransportClient transport) {
        return new AuthService(transport, _factory.CreateLogin(), _publicKeys, _hostKeys);
    }
}

internal interface IAuthService : ISshService {
    public new const string ServiceName = "ssh-userauth";
    public bool TryGetRequestHandler([NotNullWhen(true)] out ISftpRequestHandler? sftpRequestHandler);
}

internal class AuthService : SshService, IAuthService {
    private readonly ITransportClient _transport;
    private readonly ISftpLoginHandler _handler;
    private readonly IProvider<IPublicKeyAlgorithm> _publicKeys;
    private readonly IProvider<IServerHostKeyAlgorithm> _hostKeys;

    public AuthService(ITransportClient transport, ISftpLoginHandler handler, IProvider<IPublicKeyAlgorithm> publicKeys, IProvider<IServerHostKeyAlgorithm> hostKeys) {
        _transport = transport;
        _handler = handler;
        _publicKeys = publicKeys;
        _hostKeys = hostKeys;
    }

    public override string ServiceName => IAuthService.ServiceName;

    ISftpRequestHandler? _returnedHandler;
    public bool TryGetRequestHandler([NotNullWhen(true)] out ISftpRequestHandler? sftpRequestHandler) {
        sftpRequestHandler = _returnedHandler;
        return sftpRequestHandler is not null;
    }

    protected override Task ReturnPacket<T>(T packet, CancellationToken cancellationToken) {
        return _transport.SendPacket(packet, cancellationToken);
    }

    protected override Task End(CancellationToken cancellationToken) {
        _transport.End();
        return Task.CompletedTask;
    }

    protected override async Task HandlePacket(Packet packet, CancellationToken cancellationToken) {
        if (!UserauthRequest.TryParse(packet.Payload, out var request)) {
            await _transport.SendUnimplemented(cancellationToken);
            return;
        }
        var result = request switch {
            UserauthRequest.Password passwordRequest => await HandlePasswordRequest(passwordRequest, cancellationToken),
            UserauthRequest.PublicKey publickeyRequest => await HandlePublicKey(publickeyRequest, cancellationToken),
            _ or UserauthRequest.Unrecognized or UserauthRequest.None => Err<ISftpRequestHandler, LoginError>(new LoginError.EmptyCredentials()),
        };
        await (result switch {
            Ok<ISftpRequestHandler, LoginError>(var handler) => HandleSuccess(handler, cancellationToken),
            Err<ISftpRequestHandler, LoginError>(var err) => err switch {
                LoginError.SignatureNotProvided(var publicKey) => SendPKOkay(publicKey, cancellationToken),
                _ => HandleError(cancellationToken),
            },
            _ => throw new InvalidEnumArgumentException()
        });
    }

    private async Task HandleError(CancellationToken cancellationToken) {
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

    private async Task HandleSuccess(ISftpRequestHandler handler, CancellationToken cancellationToken) {
        _returnedHandler = handler;
        var reply = new UserauthSuccess();
        await ReturnPacket(reply, cancellationToken);

    }

    private async Task<Result<ISftpRequestHandler, LoginError>> HandlePublicKey(UserauthRequest.PublicKey publickeyRequest, CancellationToken cancellationToken) {
        var publicKeyAlg = _publicKeys.Items.FirstOrDefault(i => i.SupportedSignatureAlgs.Select(n => n.ToString()).Contains(publickeyRequest.AlgName));
        if (publicKeyAlg is null
            || !publicKeyAlg.TryParse(publickeyRequest.PublicKeyBytes, out var key))
            return Err<ISftpRequestHandler, LoginError>(new LoginError.WrongCredentials());

        foreach (var hostKeyAlg in _hostKeys.Items) {
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
