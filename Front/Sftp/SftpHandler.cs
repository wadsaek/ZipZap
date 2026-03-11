// SftpHandler.cs - Part of the ZipZap project for storing files online
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

using System.Threading;
using System.Threading.Tasks;

using ZipZap.Sftp.Sftp.Numbers;

using ZipZap.Classes;
using ZipZap.Front.Factories;
using ZipZap.Front.Services;
using ZipZap.LangExt.Helpers;
using ZipZap.Sftp;
using ZipZap.Sftp.Sftp;
using ZipZap.Sftp.Ssh.Algorithms;

using LoginError = ZipZap.Sftp.LoginError;

namespace ZipZap.Front;

internal class SftpHandler : ISftpLoginHandler, ISftpRequestHandler {
    private readonly IBackendFactory _backendFactory;
    private readonly ILoginService _login;

    private IBackend? _backend = null;

    public SftpHandler(IBackendFactory backendFactory, ILoginService login) {
        _backendFactory = backendFactory;
        _login = login;
    }

    public Task<Result<ISftpRequestHandler, LoginError>> TryLoginPublicKey(string username, IPublicKey userPublicKey, IHostKeyPair serverHostKey, CancellationToken cancellationToken) {
        return TryLoginPublicKeyRaw(3, username, userPublicKey, serverHostKey, cancellationToken);
    }
    private async Task<Result<ISftpRequestHandler, LoginError>> TryLoginPublicKeyRaw(uint triesLeft, string username, IPublicKey userPublicKey, IHostKeyPair serverHostKey, CancellationToken cancellationToken) {
        if (triesLeft == 0) return new Err<ISftpRequestHandler, LoginError>(new LoginError.Other());
        var result = await _login.LoginSsh(username, userPublicKey, serverHostKey, cancellationToken);
        return await result
        .Select(token => {
            _backend = _backendFactory.Create(new(token));
            return this as ISftpRequestHandler;
        })
        .ErrSelectManyAsync(async error => {
            if (error is SshLoginError.TimestampTooEarly or SshLoginError.TimestampWasUsed)
                return await TryLoginPublicKeyRaw(triesLeft - 1, username, userPublicKey, serverHostKey, cancellationToken);
            LoginError returned = error switch {
                SshLoginError.EmptyUsername => new LoginError.EmptyCredentials(),
                SshLoginError.UserPublicKeyDoesntMatch => new LoginError.WrongCredentials(),
                SshLoginError.HostPublicKeyNotAuthorized => new LoginError.HostPublicKeyNotAuthorized(),
                _ or SshLoginError.Other => new LoginError.Other()
            };
            return new Err<ISftpRequestHandler, LoginError>(returned);
        });
    }

    public async Task<Result<ISftpRequestHandler, LoginError>> TryLoginPassword(string username, string password, CancellationToken cancellationToken) {
        var result = await _login.Login(username, password, cancellationToken);
        return result
        .Select(token => {
            _backend = _backendFactory.Create(new(token));
            return this as ISftpRequestHandler;
        })
        .SelectErr(error => error switch {
            Services.LoginError.EmptyCredentials => new LoginError.EmptyCredentials() as LoginError,
            Services.LoginError.WrongCredentials => new LoginError.WrongCredentials(),
            _ => new LoginError.Other()
        });
    }

    public async Task<Result<ZipZap.Sftp.Handle, Status>> Open(string pathName, OpenFlags flags, FileAttributes attributes, CancellationToken cancellationToken) {
        return new Err<ZipZap.Sftp.Handle, Status>(new Status(SftpError.OpUnsupported, "This operation is not supported"));
    }

    public async Task<Status> Close(ZipZap.Sftp.Handle handle, CancellationToken cancellationToken) {
        return new Status(SftpError.OpUnsupported, "This operation is not supported");
    }

    public async Task<Result<byte[], Status>> Read(ZipZap.Sftp.Handle handle, ulong offset, uint length, CancellationToken cancellationToken) {
        return new Err<byte[], Status>(new Status(SftpError.OpUnsupported, "This operation is not supported"));
    }

    public async Task<Status> Write(ZipZap.Sftp.Handle handle, ulong offset, byte[] data, CancellationToken cancellationToken) {
        return new Status(SftpError.OpUnsupported, "This operation is not supported");
    }

    public async Task<Status> Remove(string path, CancellationToken cancellationToken) {
        return new Status(SftpError.OpUnsupported, "This operation is not supported");
    }

    public async Task<Status> Rename(string oldpath, string newpath, CancellationToken cancellationToken) {
        return new Status(SftpError.OpUnsupported, "This operation is not supported");
    }

    public async Task<Status> MkDir(string path, FileAttributes fileAttributes, CancellationToken cancellationToken) {
        return new Status(SftpError.OpUnsupported, "This operation is not supported");
    }

    public async Task<Status> RmDir(string path, CancellationToken cancellationToken) {
        return new Status(SftpError.OpUnsupported, "This operation is not supported");
    }

    public async Task<Result<ZipZap.Sftp.Handle, Status>> OpenDir(string path, CancellationToken cancellationToken) {
        return new Err<ZipZap.Sftp.Handle, Status>(new Status(SftpError.OpUnsupported, "This operation is not supported"));
    }

    public async Task<Result<FileName[], Status>> ReadDir(string path, CancellationToken cancellationToken) {
        return new Err<FileName[], Status>(new Status(SftpError.OpUnsupported, "This operation is not supported"));
    }

    public async Task<Result<FileAttributes, Status>> Stat(string path, CancellationToken cancellationToken) {
        return new Err<FileAttributes, Status>(new Status(SftpError.OpUnsupported, "This operation is not supported"));
    }

    public async Task<Result<FileAttributes, Status>> LStat(string path, CancellationToken cancellationToken) {
        return new Err<FileAttributes, Status>(new Status(SftpError.OpUnsupported, "This operation is not supported"));
    }

    public async Task<Result<FileAttributes, Status>> FStat(ZipZap.Sftp.Handle handle, CancellationToken cancellationToken) {
        return new Err<FileAttributes, Status>(new Status(SftpError.OpUnsupported, "This operation is not supported"));
    }

    public async Task<Status> SetStat(string path, FileAttributes fileAttributes, CancellationToken cancellationToken) {
        return new Status(SftpError.OpUnsupported, "This operation is not supported");
    }

    public async Task<Status> LSetStat(string path, FileAttributes fileAttributes, CancellationToken cancellationToken) {
        return new Status(SftpError.OpUnsupported, "This operation is not supported");
    }

    public async Task<Status> FSetStat(ZipZap.Sftp.Handle hanle, FileAttributes fileAttributes, CancellationToken cancellationToken) {
        return new Status(SftpError.OpUnsupported, "This operation is not supported");
    }

    public async Task<Result<FileName, Status>> Readlink(string path, CancellationToken cancellationToken) {
        return new Err<FileName, Status>(new Status(SftpError.OpUnsupported, "This operation is not supported"));
    }

    public async Task<Status> Symlink(string linkpath, string targetpath, CancellationToken cancellationToken) {
        return new Status(SftpError.OpUnsupported, "This operation is not supported");
    }

    public async Task<Result<FileName, Status>> RealPath(string path, CancellationToken cancellationToken) {
        return new Err<FileName, Status>(new Status(SftpError.OpUnsupported, "This operation is not supported"));
    }
}

