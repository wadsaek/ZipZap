// SftpFileHandler.cs - Part of the ZipZap project for storing files online
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Google.Protobuf;

using Microsoft.Extensions.Logging;

using ZipZap.Classes;
using ZipZap.Front.Services;
using ZipZap.LangExt.Helpers;
using ZipZap.Sftp;
using ZipZap.Sftp.Sftp;
using ZipZap.Sftp.Sftp.Numbers;

using static ZipZap.LangExt.Helpers.ResultConstructor;

namespace ZipZap.Front.Sftp;

class SftpFileHandler {
    private readonly HandleStore _handleStore;
    private readonly IBackend _backend;

    public SftpFileHandler(IBackend backend, HandleStore handleStore) {
        _handleStore = handleStore;
        _backend = backend;
    }

    // well this is a clusterdump ain't it now
    public Task<Result<Handle, Status>> Open(string pathName, OpenFlags flags, FileAttributes attributes, CancellationToken cancellationToken) {
        return _backend.GetFsoByPathAsync(new PathDataWithPath(pathName), cancellationToken)
        .CheckFlags(_backend , pathName, flags, attributes, cancellationToken)
        .FollowSymlinks(_backend, pathName, cancellationToken)
        .SelectAsync(fso => _handleStore.CreateHandle(new OpenFileData.FileData(
            fso.Id,
            IsReadable: flags.HasFlag(OpenFlags.Read),
            IsWriteable: flags.HasFlag(OpenFlags.Write)
        )))
        .SelectErrAsync(err => err.ToStatus());
    }

    public async Task<Result<byte[], Status>> Read(Handle handle, ulong offset, uint length, CancellationToken cancellationToken) {
        if (!_handleStore.TryGetFileData(handle, out var fileData)) {

            return Err<byte[], Status>(SftpHandler.HandleDoesntExist);
        }
        if (!fileData.IsReadable) {
            return Err<byte[], Status>(new(SftpError.OpUnsupported, "Not opened with Read flag"));
        }
        var result1 = await _backend.GetFsoByIdAsync(fileData.Id, cancellationToken);
        var resutl2 = result1
            .Select(fso =>
                    fso is File file
                    ? file.Content?.Skip((int)offset).Take((int)length).ToArray() ?? []
                    : []);
        var bytes = resutl2
            .SelectErr(err => err.ToStatus());
        return bytes switch {

            Ok<byte[], Status>([]) => Err<byte[], Status>(new(SftpError.Eof, "Reached the end")),
            Ok<byte[], Status>(var by) => Ok<byte[], Status>(by),
            _ => bytes
        };
    }

    public async Task<Status> Write(Handle handle, ulong offset, byte[] data, CancellationToken cancellationToken) {
        if (!_handleStore.TryGetFileData(handle, out var fileData))
            return SftpHandler.HandleDoesntExist;
        if (!fileData.IsWriteable)
            return new(SftpError.OpUnsupported, "Not opened with Write flag");
        if ((long)offset + data.LongLength > FileSize.FromMegaBytes(16).Bytes)
            return new(SftpError.Failure, "File too long");
        return await _backend.GetFsoByIdAsync(fileData.Id, cancellationToken)
            .SelectErrAsync(err => err.ToStatus())
            .FilterFileTypeAsync<File>()
            .SelectAsync(file => file.Content)
            .SelectAsync(bytes => {
                bytes ??= [];
                if (bytes.LongLength < (long)offset + data.LongLength) {
                    var newArr = new byte[(long)offset + data.LongLength];
                    bytes.CopyTo(newArr);
                    bytes = newArr;
                }
                data.CopyTo(bytes, (long)offset);
                return bytes;
            })
            .SelectManyAsync(bytes =>
                _backend.ReplaceFileById(fileData.Id, ByteString.CopyFrom(bytes), cancellationToken)
                .SelectErrAsync(err => err.ToStatus())
                )
            .SelectAsync(_ => new Status(SftpError.Ok, "Done!"))
            .UnwrapOrElseAsync(status => status);
    }

    public Task<Status> Remove(string path, CancellationToken cancellationToken) {
        return _backend.GetFsoByPathAsync(new PathDataWithPath(path), cancellationToken)
            .SelectManyAsync(fso => _backend.DeleteFso(fso.Id, DeleteOptions.AllExceptDirectories, cancellationToken))
            .SelectAsync(_ => new Status(SftpError.Ok, "Done!"))
            .UnwrapOrElseAsync(err => err.ToStatus());
    }
}
