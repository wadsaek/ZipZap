// SshService.cs - Part of the ZipZap project for storing files online
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

using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Google.Protobuf.WellKnownTypes;

using ZipZap.Classes.Helpers;
using ZipZap.Persistence.Repositories;

namespace ZipZap.FileService.Services;

public class SshService : ISshService {
    private readonly IUserRepository _userRepository;
    private readonly IUserSshKeysRepository _userSshRepo;
    private readonly ITrustedAuthorityKeysRepository _trustedServerKeysRepo;
    private readonly ITokenService _tokenService;

    public SshService(
        IUserSshKeysRepository userSshRepo,
        IUserRepository userRepository,
        ITokenService tokenService,
        ITrustedAuthorityKeysRepository trustedServerKeysRepo
    ) {
        _userSshRepo = userSshRepo;
        _userRepository = userRepository;
        _tokenService = tokenService;
        _trustedServerKeysRepo = trustedServerKeysRepo;
    }

    public async Task<Result<string, SshLoginError>> LoginSsh(string username, SshPublicKey userPublicKey, SshPublicKey hostPublicKey, Timestamp timestamp, byte[] Signature, CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(username))
            return Err<string, SshLoginError>(new SshLoginError.EmptyUsername());

        var timestampString = timestamp.ToString();
        var userBytes = userPublicKey.ToSshByteString();
        var hostBytes = hostPublicKey.ToSshByteString();
        if (hostBytes is null) return Err<string, SshLoginError>(new SshLoginError.HostPublicKeyNotAuthorized());
        if (userBytes is null) return Err<string, SshLoginError>(new SshLoginError.UserPublicKeyDoesntMatch());

        var keysResult = await _userSshRepo.GetForUsername(username, cancellationToken);

        var storedKey = keysResult.FirstOrDefault(
            k => k.Key.ToSshByteString()?.SequenceEqual(userBytes) ?? false
        );
        if (storedKey is null) return Err<string, SshLoginError>(new SshLoginError.UserPublicKeyDoesntMatch());


        var serverKeys = await _trustedServerKeysRepo.GetAll(cancellationToken);
        var serverKey = serverKeys.FirstOrDefault(
            k => k.Key.ToSshByteString()?.SequenceEqual(hostBytes) ?? false
        );
        if (serverKey is null) return Err<string, SshLoginError>(new SshLoginError.HostPublicKeyNotAuthorized());

        var unsigned = new Sftp.Ssh.SshMessageBuilder()
            .Write(username)
            .WriteByteString(userBytes)
            .WriteByteString(hostBytes)
            .Write(timestampString)
            .Build();

        var result = hostPublicKey.VerifySignature(Signature, unsigned);

        switch (result) {
            case VerifySignatureResult.Ok:
                break;
            case VerifySignatureResult.BadKey:
                return Err<string, SshLoginError>(new SshLoginError.HostPublicKeyNotAuthorized());
            case VerifySignatureResult.BadSignature:
                return Err<string, SshLoginError>(new SshLoginError.BadSignature());
            default: throw new InvalidEnumArgumentException();
        }
        var user = storedKey.User switch {
            ExistsEntity<User, UserId>(var u) => u,
            OnlyId<User, UserId>(var id) => (await _userRepository.GetByIdAsync(id, cancellationToken))!,
            _ => throw new InvalidEnumArgumentException()
        };

        return Ok<string, SshLoginError>(_tokenService.GenerateToken(user));
    }
}

