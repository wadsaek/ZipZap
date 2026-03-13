// SftpPathsHandler.cs - Part of the ZipZap project for storing files online
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

using ZipZap.Classes;
using ZipZap.Classes.Extensions;
using ZipZap.Front.Helpers;
using ZipZap.Front.Services;
using ZipZap.LangExt.Extensions;
using ZipZap.LangExt.Helpers;
using ZipZap.Sftp;
using ZipZap.Sftp.Sftp;
using ZipZap.Sftp.Sftp.Numbers;

namespace ZipZap.Front.Sftp;

class SftpPathsHandler {
    private readonly IBackend _backend;

    public SftpPathsHandler(IBackend backend) {
        _backend = backend;
    }

    public async Task<Result<FileName, Status>> Readlink(string path, CancellationToken cancellationToken) {
        return await _backend.GetFsoByPathAsync(new PathDataWithPath(path), cancellationToken)
        .SelectErrAsync(err => err.ToStatus())
        .FilterFileTypeAsync<Symlink>()
        .SelectAsync(link => link.Target)
        .SelectAsync(t => new FileName(t, t, FileAttributes.Empty));
    }

    public Task<Status> Symlink(string linkpath, string targetpath, CancellationToken cancellationToken) {
        var linkparts = linkpath.SplitPath().ToArray();
        var linkdir = linkparts[..^1].ConcatenateWith("/");
        var linkname = linkparts[^1];

        return _backend.GetFsoByPathAsync(new PathDataWithPath(linkdir), cancellationToken)
        .SelectAsync(fso => fso.Id)
        .WithUser(_backend, cancellationToken)
        .SelectAsync(param => {
            var (parentId, User) = param;
            var data = new FsData(
                    parentId,
                    Permissions.SymlinkDefault,
                    linkname,
                    User.DefaultOwnership);
            return new Symlink(default, data, targetpath);
        })
        .SelectManyAsync(symlink => _backend.MakeLink(symlink, cancellationToken))
        .SelectAsync(_ => new Status(SftpError.Ok, "Done!"))
        .UnwrapOrElseAsync(err => err.ToStatus());
    }

    public async Task<Result<FileName, Status>> RealPath(string path, CancellationToken cancellationToken) {
        var realpath = PathHelper.NormalizePath(path, []).ConcatenateWith("/");
        if (string.IsNullOrWhiteSpace(realpath)) realpath = "/";
        var fso = await _backend.GetFsoByPathAsync(new PathDataWithPath(realpath), cancellationToken);
        return fso
        .Select(fso => ToName(fso, realpath))
        .SelectErr(err => err.ToStatus());
    }
    private static FileName ToName(Fso fso, string name) => new(name, fso.ToString(Fso.LongListingFormat), fso.ToAttrs(true));
}
