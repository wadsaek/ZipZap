// FsoSftpExtensions.cs - Part of the ZipZap project for storing files online
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

using ZipZap.Sftp.Sftp.Numbers;

using ZipZap.Classes;
using ZipZap.Front.Services;
using ZipZap.Sftp;
using ZipZap.Sftp.Sftp;
using ZipZap.LangExt.Helpers;
using ZipZap.Classes.Extensions;

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ZipZap.Front.Helpers;
using ZipZap.LangExt.Extensions;
using static ZipZap.LangExt.Helpers.ResultConstructor;
using System.ComponentModel;

using Google.Protobuf;
using Microsoft.Extensions.Logging;


namespace ZipZap.Front;

using Ownership = Classes.Ownership;
using SftpOwnership = ZipZap.Sftp.Sftp.Ownership;
using UnixFileMode = System.IO.UnixFileMode;

using static ServiceError;

static class FsoSftpExtensions {
    extension(Fso fso) {
        public FileName ToName(bool ignoreSize)
            => new(fso.Data.Name, fso.ToString(Fso.LongListingFormat), fso.ToAttrs(ignoreSize));
        public FileAttributes ToAttrs(bool ignoreSize) {
            return fso switch {
                File { Content: var content }
                    => fso.Data.ToAttrs() with {
                        Size = ignoreSize ? null : (ulong?)content?.Length,
                        Permissions = fso.Data.Permissions.Inner | UnixFileMode.RegularFile
                    },
                Symlink
                    => fso.Data.ToAttrs() with { Permissions = fso.Data.Permissions.Inner | UnixFileMode.Symlink },
                Directory
                    => fso.Data.ToAttrs() with { Permissions = fso.Data.Permissions.Inner | UnixFileMode.Directory },
                _ => throw new InvalidEnumArgumentException()
            };
        }
    }
    extension(FsData data) {
        public FileAttributes ToAttrs() {
            return new(null, data.Ownership.ToSftpOwnership(), data.Permissions.Inner, null, []);
        }
    }
    extension(Ownership ownership) {
        public SftpOwnership ToSftpOwnership()
            => new((uint)ownership.FsoOwner, (uint)ownership.FsoGroup);
    }
    extension(SftpOwnership ownership) {
        public Ownership ToOwnership()
            => new((int)ownership.UserId, (int)ownership.GroupId);
    }
    extension(ServiceError error) {
        public Status ToStatus() => error switch {
            FailedPrecondition(var detail) => new(SftpError.Failure, detail),
            Unauthorized => new(SftpError.PermissionDenied, "Unauthorized"),
            NotFound => new(SftpError.NoSuchFile, "Not found"),
            BadResult => new(SftpError.Failure, "Got a bad result"),
            BadRequest(var detail) => new Status(SftpError.BadMessage, detail),
            AlreadyExists => new(SftpError.Failure, "File already exists"),
            Unknown(var exception) => new(SftpError.Failure, $"Internal server error({exception.Message})"),
            _ => new(SftpError.Failure, $"Unknown error ({error})")
        };
    }
    extension(FileAttributes attrs) {
        public FsData ToFsData(Ownership fallbackOwnership, Permissions fallbackPermissions, string name, FsoId parentId)
            => new(
                parentId,
                attrs.Permissions is not null
                    ? new((UnixFileMode)attrs.Permissions)
                    : fallbackPermissions,
                name,
                attrs.Ownership?.ToOwnership() ?? fallbackOwnership);
    }
    extension(Task<Result<Fso, ServiceError>> resultTask) {
        public async Task<Result<Fso, ServiceError>> FollowSymlinks(IBackend backend, string pathName, CancellationToken cancellationToken) {
            var result = await resultTask;
            while (result is Ok<Fso, ServiceError>(Symlink { Target: var target })) {
                result = await result.SelectManyAsync(fso => {
                    var parts = pathName.SplitPath().SkipLast(1);
                    var targetPath = PathHelper.NormalizePath(target, parts).ConcatenateWith("/");
                    return backend.GetFsoByPathAsync(new PathDataWithPath(targetPath), cancellationToken);
                });
            }
            return result;
        }
        public Task<Result<Fso, ServiceError>> CheckFlags(IBackend backend, string pathName, OpenFlags flags, FileAttributes attrs, CancellationToken cancellationToken) =>
            resultTask
        .SelectManyAsync(fso => {
            if (flags.HasFlag(OpenFlags.Creat | OpenFlags.Excl))
                return Task.FromResult(Err<Fso, ServiceError>(new AlreadyExists()));
            if (flags.HasFlag(OpenFlags.Creat | OpenFlags.Trunc) && fso is not Directory)
                return backend.ReplaceFileById(fso.Id, ByteString.Empty, cancellationToken).SelectAsync(_ => fso);
            return Task.FromResult(Ok<Fso, ServiceError>(fso));
        })
            .ErrSelectManyAsync(err => {
                if (err is not NotFound || !flags.HasFlag(OpenFlags.Creat))
                    return Task.FromResult(Err<Fso, ServiceError>(err));
                var pathsplit = pathName.SplitPath().ToArray();
                var dirname = pathsplit[..^1].ConcatenateWith("/");
                var filename = pathsplit[^1];
                return backend.GetSelf(cancellationToken)
                .SelectManyAsync(user =>
                        backend.GetFsoByPathAsync(new PathDataWithPath(dirname), cancellationToken)
                        .SelectAsync(fso => (fso.Id, user))
                )
                .SelectManyAsync(param => {
                    var (parentId, user) = param;
                    return backend.SaveFile(
                        ByteString.Empty,
                        new(
                            default,
                            attrs.ToFsData(
                                user.DefaultOwnership,
                                Permissions.FileDefault,
                                pathName.SplitPath().Last(),
                                parentId
                            )),
                        cancellationToken
                    )
                    .SelectAsync(file => file as Fso);
                });
            });
    }
    extension(Result<Fso, Status> result) {
        // ?????
        // For some godforsaken reason using `SelectMany` here doesn't work
        // EVEN THOUGH IT'S SUPPOSED TO BE THE EXACT SAME
        public Result<T, Status> FilterFileType<T>()
            where T : Fso
            => result switch {
                Err<Fso, Status>(var status) => Err<T, Status>(status),
                Ok<Fso, Status>(var fso) => fso is T dir
                    ? Ok<T, Status>(dir)
                    : Err<T, Status>(new(
                        SftpError.PermissionDenied,
                        "This is not a directory"
                    )),
                _ => throw new InvalidEnumArgumentException()
            };
        public Result<Directory, Status> FilterDirectorySelectMany()
            => result
            .SelectMany(fso => fso is Directory dir
                ? Ok<Directory, Status>(dir)
                : Err<Directory, Status>(new(
                    SftpError.PermissionDenied,
                    "This is not a directory")));

    }
    extension(Task<Result<Fso, Status>> resultTask) {
        public async Task<Result<T, Status>> FilterFileTypeAsync<T>()
            where T : Fso
            => (await resultTask).FilterFileType<T>();

    }
    extension<T>(Task<Result<T, ServiceError>> result) {
        public Task<Result<(T, User), ServiceError>> WithUser(IBackend backend, CancellationToken cancellationToken)
            => result.SelectManyAsync(t =>
                    backend
                        .GetSelf(cancellationToken)
                        .SelectAsync(user => (t, user))
                );
    }
}

