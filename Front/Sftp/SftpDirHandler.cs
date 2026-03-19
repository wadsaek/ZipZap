// SftpDirHandler.cs - Part of the ZipZap project for storing files online
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
using ZipZap.Front.Services;
using ZipZap.LangExt.Helpers;
using ZipZap.Sftp;
using ZipZap.Sftp.Sftp;
using System.Linq;
using static ZipZap.LangExt.Helpers.ResultConstructor;
using ZipZap.Classes.Extensions;
using ZipZap.LangExt.Extensions;

namespace ZipZap.Front.Sftp;

class SftpDirHandler {
    private readonly IBackend _backend;
    private readonly HandleStore _handleStore;

    public SftpDirHandler(IBackend backend, HandleStore handleStore) {
        _backend = backend;
        _handleStore = handleStore;
    }

    public Task<Status> MkDir(string path, FileAttributes fileAttributes, CancellationToken cancellationToken) {
        var pathsplit = path.SplitPath().ToArray();
        var parentDirname = pathsplit[..^1].ConcatenateWith("/");
        var filename = pathsplit[^1];
        return _backend.GetFsoByPathAsync(new PathDataWithPath(parentDirname), cancellationToken)
        .SelectAsync(f => f.Fso)
        .WithUser(_backend, cancellationToken)
        .SelectAsync(param => {
            var (parent, user) = param;
            var dir = new Directory(default, fileAttributes.ToFsData(
                user.DefaultOwnership,
                Permissions.DirectoryDefault,
                filename,
                parent.Id
            ));
            return dir;
        })
        .SelectManyAsync(dir => _backend.MakeDirectory(dir, cancellationToken))
        .SelectAsync(_ => new Status(SftpError.Ok, "Done!"))
        .UnwrapOrElseAsync(err => err.ToStatus());
    }

    public Task<Status> RmDir(string path, CancellationToken cancellationToken) {
        return _backend.GetFsoByPathAsync(new PathDataWithPath(path), cancellationToken)
        .SelectAsync(f => f.Fso)
        .SelectManyAsync(fso => _backend.DeleteFso(fso.Id, DeleteOptions.OnlyEmptyDirectories))
        .SelectAsync(_ => new Status(SftpError.Ok, "Done!"))
        .UnwrapOrElseAsync(err => err.ToStatus());
    }

    public async Task<Result<Handle, Status>> OpenDir(string path, CancellationToken cancellationToken) {
        var fsoResult = await _backend.GetFsoByPathAsync(new PathDataWithPath(path), cancellationToken)
            .SelectAsync(f => f.Fso)
            .FollowSymlinks(_backend, path, cancellationToken)
            .SelectErrAsync(err => err.ToStatus());
        return fsoResult
            .FilterFileType<Directory>()
            .Select(fso => _handleStore.CreateHandle(new OpenFileData.DirectoryData(fso.Id, false)));
    }

    public async Task<Result<FileName[], Status>> ReadDir(Handle handle, CancellationToken cancellationToken) {
        if (!_handleStore.TryGetDirData(handle, out var dirData))
            return Err<FileName[], Status>(SftpHandler.HandleDoesntExist);
        if (dirData.HasBeenRead) return Err<FileName[], Status>(new(SftpError.Eof, "already read"));

        var fsoResult = await _backend.GetFsoByIdAsync(dirData.Id, cancellationToken)
        .SelectErrAsync(err => err.ToStatus());
        return fsoResult
        .Select(f => f.Fso)
        .FilterFileType<Directory>()
        .Select(dir => dir.MaybeChildren.Select(d => d.ToName(true)).ToArray())
        .Select(dir => {
            _handleStore[handle] = dirData with { HasBeenRead = true };
            return dir;
        });
    }
}

