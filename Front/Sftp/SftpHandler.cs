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

using Microsoft.Extensions.Logging;

using ZipZap.Classes;
using ZipZap.Front.Services;
using ZipZap.LangExt.Helpers;
using ZipZap.Sftp;
using ZipZap.Sftp.Sftp;
using ZipZap.Sftp.Sftp.Numbers;

namespace ZipZap.Front.Sftp;

class SftpHandler : ISftpRequestHandler {
    public const string HandleDoesntExistMessage = "This handle doesn't exist. Maybe it was already deleted?";
    public static Status HandleDoesntExist => new(SftpError.NoSuchFile, HandleDoesntExistMessage);

    private readonly ILogger<SftpHandler> _logger;
    private readonly SftpPathsHandler _pathHandler;
    private readonly SftpFileHandler _fileHandler;
    private readonly SftpStatHandler _statHandler;
    private readonly SftpDirHandler _dirHandler;
    private readonly IBackend _backend;
    private readonly HandleStore _handleStore;

    public SftpHandler(IBackend backend, ILogger<SftpHandler> logger, IFsoService fsoService) {
        _backend = backend;
        _logger = logger;
        _handleStore = new();
        _statHandler = new(_backend, _handleStore, fsoService);
        _pathHandler = new(_backend);
        _fileHandler = new(_backend, _handleStore);
        _dirHandler = new(_backend, _handleStore);
    }


    public Task<Status> Close(Handle handle, CancellationToken cancellationToken) {
        return Task.FromResult(_handleStore.Remove(handle)
            ? new Status(SftpError.Ok, "Done!")
            : HandleDoesntExist);
    }


    public Task<Status> Rename(string oldpath, string newpath, CancellationToken cancellationToken) {
        return _statHandler.Rename(oldpath, newpath, cancellationToken);
    }

    public Task<Result<FileAttributes, Status>> Stat(string path, CancellationToken cancellationToken) {
        return _statHandler.Stat(path, cancellationToken);
    }

    public Task<Result<FileAttributes, Status>> LStat(string path, CancellationToken cancellationToken) {
        return _statHandler.LStat(path, cancellationToken);
    }

    public Task<Result<FileAttributes, Status>> FStat(Handle handle, CancellationToken cancellationToken) {
        return _statHandler.FStat(handle, cancellationToken);
    }

    public Task<Status> SetStat(string path, FileAttributes fileAttributes, CancellationToken cancellationToken) {
        return _statHandler.SetStat(path, fileAttributes, cancellationToken);
    }

    public Task<Status> LSetStat(string path, FileAttributes fileAttributes, CancellationToken cancellationToken) {
        return _statHandler.LSetStat(path, fileAttributes, cancellationToken);
    }

    public Task<Status> FSetStat(Handle handle, FileAttributes fileAttributes, CancellationToken cancellationToken) {
        return _statHandler.FSetStat(handle, fileAttributes, cancellationToken);
    }

    public Task<Result<FileName, Status>> Readlink(string path, CancellationToken cancellationToken) {
        return _pathHandler.Readlink(path, cancellationToken);
    }

    public Task<Status> Symlink(string linkpath, string targetpath, CancellationToken cancellationToken) {
        return _pathHandler.Symlink(linkpath, targetpath, cancellationToken);
    }

    public Task<Result<FileName, Status>> RealPath(string path, CancellationToken cancellationToken) {
        return _pathHandler.RealPath(path, cancellationToken);
    }

    public Task<Result<byte[], Status>> Read(Handle handle, ulong offset, uint length, CancellationToken cancellationToken) {
        return _fileHandler.Read(handle, offset, length, cancellationToken);
    }

    public Task<Status> Write(Handle handle, ulong offset, byte[] data, CancellationToken cancellationToken) {
        return _fileHandler.Write(handle, offset, data, cancellationToken);
    }

    public Task<Result<Handle, Status>> Open(string filename, OpenFlags flags, FileAttributes attributes, CancellationToken cancellationToken) {
        return _fileHandler.Open(filename, flags, attributes, cancellationToken);
    }
    public Task<Status> Remove(string path, CancellationToken cancellationToken) {
        return _fileHandler.Remove(path, cancellationToken);
    }

    public Task<Status> MkDir(string path, FileAttributes fileAttributes, CancellationToken cancellationToken) {
        return _dirHandler.MkDir(path, fileAttributes, cancellationToken);
    }

    public Task<Status> RmDir(string path, CancellationToken cancellationToken) {
        return _dirHandler.RmDir(path, cancellationToken);
    }

    public Task<Result<Handle, Status>> OpenDir(string path, CancellationToken cancellationToken) {
        return _dirHandler.OpenDir(path, cancellationToken);
    }

    public Task<Result<FileName[], Status>> ReadDir(Handle handle, CancellationToken cancellationToken) {
        return _dirHandler.ReadDir(handle, cancellationToken);
    }

}

abstract record OpenFileData(FsoId Id) {
    public sealed record FileData(FsoId Id, bool IsReadable, bool IsWriteable) : OpenFileData(Id);
    public sealed record DirectoryData(FsoId Id, bool HasBeenRead) : OpenFileData(Id);
}
