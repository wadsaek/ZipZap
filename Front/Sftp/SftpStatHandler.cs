// SftpStatHandler.cs - Part of the ZipZap project for storing files online
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

using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using ZipZap.Classes;
using ZipZap.Classes.Extensions;
using ZipZap.Front.Services;
using ZipZap.LangExt.Extensions;
using ZipZap.LangExt.Helpers;
using ZipZap.Sftp;
using ZipZap.Sftp.Sftp;
using ZipZap.Sftp.Sftp.Numbers;

using static ZipZap.LangExt.Helpers.ResultConstructor;

namespace ZipZap.Front.Sftp;

class SftpStatHandler {
    private readonly IBackend _backend;
    private readonly HandleStore _handleStore;

    public SftpStatHandler(IBackend backend, HandleStore handleStore) {
        _backend = backend;
        _handleStore = handleStore;
    }

    public Task<Result<FileAttributes, Status>> Stat(string path, CancellationToken cancellationToken) {
        return _backend.GetFsoByPathAsync(new PathDataWithPath(path), cancellationToken)
        .FollowSymlinks(_backend, path, cancellationToken)
        .SelectAsync(fso => fso.ToAttrs(false))
        .SelectErrAsync(err => err.ToStatus());
    }

    public Task<Result<FileAttributes, Status>> LStat(string path, CancellationToken cancellationToken) {
        return _backend.GetFsoByPathAsync(new PathDataWithPath(path), cancellationToken)
        .SelectAsync(fso => fso.ToAttrs(false))
        .SelectErrAsync(err => err.ToStatus());
    }

    public Task<Result<FileAttributes, Status>> FStat(Handle handle, CancellationToken cancellationToken) {
        if (!_handleStore.TryGetFileData(handle, out var data))
            return Task.FromResult(Err<FileAttributes, Status>(SftpHandler.HandleDoesntExist));
        return _backend.GetFsoByIdAsync(data.Id, cancellationToken)
            .SelectAsync(data => data.ToAttrs(false))
            .SelectErrAsync(err => err.ToStatus());
    }

    public async Task<Status> SetStat(string path, FileAttributes fileAttributes, CancellationToken cancellationToken) {
        var fso = await _backend.GetFsoByPathAsync(new PathDataWithPath(path), cancellationToken)
            .FollowSymlinks(_backend, path, cancellationToken);

        return await ChangePermissions(fso, fileAttributes, cancellationToken);

    }
    private Task<Status> ChangePermissions(Result<Fso, ServiceError> result, FileAttributes fileAttributes, CancellationToken cancellationToken) {
        return result

        .Select(fso => fso with {
            Data = fileAttributes.ToFsData(
            fso.Data.Ownership,
            fso.Data.Permissions,
            fso.Data.Name,
            fso.Data.VirtualLocation!)
        })
        .SelectManyAsync(fso => _backend.UpdateFso(fso, cancellationToken))
        .SelectAsync(_ => new Status(SftpError.Ok, "Done!"))
        .UnwrapOrElseAsync(err => err.ToStatus());
    }

    public async Task<Status> LSetStat(string path, FileAttributes fileAttributes, CancellationToken cancellationToken) {
        var fso = await _backend.GetFsoByPathAsync(new PathDataWithPath(path), cancellationToken);

        return await ChangePermissions(fso, fileAttributes, cancellationToken);
    }

    public async Task<Status> FSetStat(Handle handle, FileAttributes fileAttributes, CancellationToken cancellationToken) {
        if (!_handleStore.TryGetFileData(handle, out var data))
            return SftpHandler.HandleDoesntExist;
        var fso = await _backend.GetFsoByIdAsync(data.Id, cancellationToken);
        return await ChangePermissions(fso, fileAttributes, cancellationToken);
    }

    internal Task<Status> Rename(string oldpath, string newpath, CancellationToken cancellationToken) {
        var pathsplit = newpath.SplitPath().ToArray();
        var newDirname = pathsplit[..^1].ConcatenateWith("/");
        var newFileName = pathsplit[^1];
        return _backend.GetFsoByPathAsync(new PathDataWithPath(oldpath), cancellationToken)
        .SelectManyAsync(current =>
            _backend
                .GetFsoByPathAsync(new PathDataWithPath(newDirname), cancellationToken)
                .SelectAsync(parent => (current, parent))
        )
        .SelectAsync(param => {
            var data = param.current.Data;
            data = data with {
                Name = newFileName,
                VirtualLocation = param.parent.Id
            };
            param.current = param.current with { Data = data };
            return param.current;
        })
        .SelectManyAsync(updated => _backend.UpdateFso(updated, cancellationToken))
        .SelectAsync(_ => new Status(SftpError.Ok, "Done!"))
        .UnwrapOrElseAsync(err => err.ToStatus());
    }
}
