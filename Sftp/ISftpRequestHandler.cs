// ISftpRequestHandler.cs - Part of the ZipZap project for storing files online
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

using ZipZap.LangExt.Helpers;
using ZipZap.Sftp.Sftp;
using ZipZap.Sftp.Sftp.Numbers;
using ZipZap.Sftp.Ssh;
using ZipZap.Sftp.Ssh.Algorithms;

namespace ZipZap.Sftp;

public interface ISftpRequestHandlerFactory {
    public ISftpLoginHandler CreateLogin();
}

public interface ISftpLoginHandler {
    public Task<Result<ISftpRequestHandler, LoginError>> TryLoginPublicKey(
        string username,
        IPublicKey userPublicKey,
        IHostKeyPair serverHostKey,
        CancellationToken cancellationToken);

    public Task<Result<ISftpRequestHandler, LoginError>> TryLoginPassword(
        string username,
        string password,
        CancellationToken cancellationToken);
}

public interface ISftpRequestHandler {
    public Task<Result<Handle, Status>> Open(
        string filename,
        OpenFlags flags,
        FileAttributes attributes,
        CancellationToken cancellationToken
    );

    public Task<Status> Close(Handle handle, CancellationToken cancellationToken);

    public Task<Result<byte[], Status>> Read(Handle handle, ulong offset, uint length, CancellationToken cancellationToken);

    public Task<Status> Write(Handle handle, ulong offset, byte[] data, CancellationToken cancellationToken);
    public Task<Status> Remove(string path, CancellationToken cancellationToken);

    public Task<Status> Rename(string oldpath, string newpath, CancellationToken cancellationToken);

    public Task<Status> MkDir(string path, FileAttributes fileAttributes, CancellationToken cancellationToken);
    public Task<Status> RmDir(string path, CancellationToken cancellationToken);

    public Task<Result<Handle, Status>> OpenDir(string path, CancellationToken cancellationToken);

    public Task<Result<FileName[], Status>> ReadDir(Handle path, CancellationToken cancellationToken);

    public Task<Result<FileAttributes, Status>> Stat(string path, CancellationToken cancellationToken);
    public Task<Result<FileAttributes, Status>> LStat(string path, CancellationToken cancellationToken);
    public Task<Result<FileAttributes, Status>> FStat(Handle handle, CancellationToken cancellationToken);
    public Task<Status> SetStat(string path, FileAttributes fileAttributes, CancellationToken cancellationToken);
    public Task<Status> LSetStat(string path, FileAttributes fileAttributes, CancellationToken cancellationToken);
    public Task<Status> FSetStat(Handle hanle, FileAttributes fileAttributes, CancellationToken cancellationToken);


    public Task<Result<FileName, Status>> Readlink(string path, CancellationToken cancellationToken);
    public Task<Status> Symlink(string linkpath, string targetpath, CancellationToken cancellationToken);

    public Task<Result<FileName, Status>> RealPath(string path, CancellationToken cancellationToken);
}

public record FileName(string Filename, string LongName, FileAttributes Attributes) {
    internal byte[] ToBytes() => new SshMessageBuilder()
       .Write(Filename)
       .Write(LongName)
       .WriteArray(Attributes.ToByteArray())
       .Build();
}

public record Status(SftpError Code, string ErrorMessage) {
    internal Sftp.Status ToStatusPacket(uint id) => new(id, Code, ErrorMessage);
}

public record Handle(string Value) {
}

public class Unit;

public abstract record LoginError {
    public sealed record HostPublicKeyNotAuthorized : LoginError {
        public override string ToString() => "Host public key is not recognized by the auth server";
    }

    public sealed record WrongCredentials : LoginError {
        public override string ToString() => "One or more fields is wrong";
    }
    public sealed record EmptyCredentials : LoginError {
        public override string ToString() => "One or more fields is empty";
    }
    public sealed record SignatureNotProvided(IPublicKey PublicKey) : LoginError {
        public override string ToString() => "Signature wasn't provided";
    }
    public sealed record Other : LoginError;
}
