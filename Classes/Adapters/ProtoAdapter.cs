// ProtoAdapter.cs - Part of the ZipZap project for storing files online
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

using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

using ZipZap.Classes.Extensions;
using ZipZap.Grpc;

using DataCase = ZipZap.Grpc.GetFsoResponse.SpecificDataOneofCase;
using Guid = System.Guid;
using IdCase = ZipZap.Grpc.PathData.IdOneofCase;

namespace ZipZap.Classes.Adapters;


public static class ProtoAdapter {
    extension(Fso fso) {
        public FsoWithType ToFsoWithType() => fso switch {
            File => new() {
                Type = FileType.RegularFile,
                Data = fso.ToRpcSharedData(),
                Id = fso.Id.Value.ToGrpcGuid()
            },
            Directory => new() {
                Type = FileType.Directory,
                Data = fso.ToRpcSharedData(),
                Id = fso.Id.Value.ToGrpcGuid()
            },
            Symlink { Target: var target } => new() {
                Type = FileType.Symlink,
                Data = fso.ToRpcSharedData(),
                Id = fso.Id.Value.ToGrpcGuid(),
                SymlinkData = new() { Target = target }
            },
            _ => throw new InvalidEnumArgumentException(nameof(fso))
        };
    }
    extension(FileData _) {
        public static FileData NewFileData(ByteString content) =>
            new() {
                Content = content
            };
    }
    extension(SymlinkData _) {
        public static SymlinkData NewSymlinkData(string target) =>
            new() {
                Target = target
            };
    }
    extension(DirectoryData data) {
        public static DirectoryData NewDirectoryData(IEnumerable<Fso> fsos) {
            var dirData = new DirectoryData();
            dirData.Entries.Add(fsos
                    .Select(fso => fso.ToFsoWithType())
                    );
            return dirData;
        }
        public IEnumerable<Fso> ToFsos() =>
            data.Entries.Select(ToFso);
    }
    extension(FsoWithType fso) {
        public Fso ToFso() => fso switch {
            { Type: FileType.RegularFile } => new File(fso.Id.ToGuid().ToFsoId(), fso.Data.ToFsData()),
            { Type: FileType.Directory } => new Directory(fso.Id.ToGuid().ToFsoId(), fso.Data.ToFsData()),
            { Type: FileType.Symlink, SymlinkData.Target: var target } => new Symlink(fso.Id.ToGuid().ToFsoId(), fso.Data.ToFsData(), target),
            { Type: FileType.Symlink } => throw new InvalidDataException("symlink doesn't have symlink data"),
            _ => throw new InvalidEnumArgumentException(nameof(fso))
        };
    }
    extension(GetFsoResponse response) {
        public static async Task<GetFsoResponse> FromFileAsync(File file, Stream stream) {
            var (shared, fileData) = await file.ToRpcResponse(stream);
            return new() {
                FsoId = file.Id.Value.ToGrpcGuid(),
                Data = shared,
                FileData = fileData
            };
        }
        public static GetFsoResponse FromDirectory(Directory dir) {
            var (shared, dirData) = dir.ToRpcResponse();
            return new() {
                FsoId = dir.Id.Value.ToGrpcGuid(),
                Data = shared,
                DirectoryData = dirData
            };
        }
        public static GetFsoResponse FromSymlink(Symlink symlink) {
            var (shared, linkData) = symlink.ToRpcResponse();
            return new() {
                FsoId = symlink.Id.Value.ToGrpcGuid(),
                Data = shared,
                SymlinkData = linkData
            };
        }
        public Fso ToFso() {
            var id = new FsoId(response.FsoId.ToGuid());
            var data = response.Data.ToFsData();
            return response.SpecificDataCase switch {
                DataCase.FileData => new File(id, data) { Content = response.FileData.Content.ToByteArray() },
                DataCase.SymlinkData => new Symlink(id, data, response.SymlinkData.Target),
                DataCase.DirectoryData => new Directory(id, data) {
                    MaybeChildren = response.DirectoryData.ToFsos()
                },
                DataCase.None or _ => throw new InvalidEnumArgumentException(nameof(response.SpecificDataCase))

            };
        }
    }
    extension(GetRootResponse response) {
        public Directory ToDirectory() => new(
                response.FsoId.ToGuid().ToFsoId(),
                response.Data.ToFsData()
                ) {
            MaybeChildren = response.DirectoryData.ToFsos()
        };
    }
    extension(Grpc.PathData data) {
        public PathData ToPathData(FsoId workingDirectory) => data.IdCase switch {
            IdCase.FilePath => new PathDataWithPath(data.FilePath),
            IdCase.ParentId => new PathDataWithId(data.Name, data.ParentId.ToGuid().ToFsoId()),
            IdCase.None or _ => new PathDataWithId(data.Name, workingDirectory)
        };
    }
    extension(PathData pathData) {
        public Grpc.PathData ToRpcPathData() {

            var grpcPathData = new Grpc.PathData {
                Name = pathData.Name
            };
            switch (pathData) {
                case PathDataWithPath { Path: var path }:
                    grpcPathData.FilePath = path;
                    break;
                case PathDataWithId { ParentId: var id }:
                    grpcPathData.ParentId = id.Value.ToGrpcGuid();
                    break;
            }

            return grpcPathData;
        }
    }
    extension(Grpc.Guid guid) {
        public Guid ToGuid() => Guid.Parse(guid.Value);
        public bool TryToGuid(out Guid result) => Guid.TryParse(guid.Value, out result);
    }
    extension(FsoSharedData fsoSharedData) {
        public FsData ToFsData() {
            return new(
                    fsoSharedData.RootId.ToGuid().ToFsoId().AsIdOf<Directory>(),
                    Permissions.FromBitMask(fsoSharedData.Permissions),
                    fsoSharedData.Name,
         fsoSharedData.Ownership.ToOwnership()
                );

        }
    }
    extension(Grpc.UserRole role) {
        public UserRole ToRole() => role switch {
            Grpc.UserRole.Admin => UserRole.Admin,
            Grpc.UserRole.User => UserRole.User,
            _ => throw new InvalidEnumArgumentException()
        };
    }
    extension(UserRole role) {
        public Grpc.UserRole ToGrpcRole() => role switch {
            UserRole.Admin => Grpc.UserRole.Admin,
            UserRole.User => Grpc.UserRole.User,
            _ => throw new InvalidEnumArgumentException()
        };
    }
    extension(Grpc.User user) {
        public User ToUser() => new(
            user.Id.ToGuid().ToUserId(),
            user.Username,
            [],
            user.Email,
            user.Role.ToRole(),
            user.DefaultOwnership.ToOwnership(),
            user.RootId.ToGuid().ToFsoId().AsIdOf<Directory>()
        );
    }
    extension(User user) {
        public Grpc.User ToGrpcUser() => new() {
            RootId = user.Root.Id.Value.ToGrpcGuid(),
            Id = user.Id.Value.ToGrpcGuid(),
            Email = user.Email,
            Username = user.Username,
            DefaultOwnership = user.DefaultOwnership.ToGrpcOwnership(),
            Role = user.Role.ToGrpcRole()
        };
    }

    extension(IEnumerable<User> users) {
        public UserList ToUserList() {
            var userlist = new UserList();
            userlist.User.AddRange(users.Select(ToGrpcUser));
            return userlist;
        }
    }
    extension(Ownership ownership) {
        public Grpc.Ownership ToGrpcOwnership() => new() { Group = ownership.FsoGroup, Owner = ownership.FsoOwner };
    }
    extension(Grpc.Ownership ownership) {
        public Ownership ToOwnership() => new(ownership.Owner, ownership.Group);
    }

    extension(SshKey key) {
        public SshPublicKey ToPublicKey() => new(key.Key);
    }
    extension(SshPublicKey key) {
        public SshKey ToGrpcSshKey() => new() { Key = key.Value };
    }

    extension(SshLoginError err) {
        public LoginSshError ToGrpcError() => err switch {
            SshLoginError.UserPublicKeyDoesntMatch or SshLoginError.EmptyUsername => LoginSshError.UserPublicKeyDoesntMatch,
            SshLoginError.HostPublicKeyNotAuthorized => LoginSshError.HostPublicKeyNotAuthorized,
            SshLoginError.BadSignature => LoginSshError.BadSignature,
            SshLoginError.TimestampTooEarly => LoginSshError.TimestampTooEarly,
            SshLoginError.TimestampWasUsed => LoginSshError.TimestampWasUsed,
            SshLoginError.Other => LoginSshError.Other,
            _ => throw new InvalidEnumArgumentException()
        };
    }
    extension(IEnumerable<UserSshKey> keys) {
        public UserSshKeyList ToSshKeyList() {
            var list = new UserSshKeyList();
            list.Keys.AddRange(keys.Select(
                static k => new Grpc.UserSshKey() {
                    Key = k.Key.ToGrpcSshKey(),
                    Id = k.Id.Id.ToGrpcGuid()
                }
            ));
            return list;
        }
    }
    extension(UserSshKeyList keys) {
        public IEnumerable<UserSshKeyRaw> ToSshKeys()
            => keys.Keys.Select(k => new UserSshKeyRaw(new(k.Id.ToGuid()), k.Key.ToPublicKey()));
    }
    extension(IEnumerable<TrustedAuthorityKeyWithUser> keys) {
        public HostKeys ToGrpcHostKeys() {
            var list = new HostKeys();
            list.Keys.Add(keys.Select(k => k.ToGrpcHostKey()));
            return list;
        }
    }
    extension(TrustedAuthorityKeyWithUser key) {
        public SshHostKeyServer ToGrpcHostKey() {
            var hostkey = new SshHostKeyServer {
                Id = key.Id.Id.ToGrpcGuid(),
                Key = key.Key.ToGrpcSshKey(),
                ServerName = key.ServerName,
                AddedAt = key.TimeAdded.ToTimestamp(),
            };
            if (key.Admin is not null)
                hostkey.AdminWhoAddedId = key.Admin?.ToGrpcUser();
            return hostkey;
        }
    }
    extension(SshHostKeyServer key) {
        public TrustedAuthorityKeyWithUser ToKey() => new(
            new(key.Id.ToGuid()),
            key.ServerName,
            key.Key.ToPublicKey(),
            key.AddedAt.ToDateTimeOffset(),
            key.AdminWhoAddedId.ToUser()
        );
    }
    extension(HostKeys keys) {
        public IEnumerable<TrustedAuthorityKeyWithUser> ToKeys() => keys.Keys.Select(ToKey);
    }
    extension(DeleteOptions opts) {
        public Grpc.DeleteOptions ToGrpcOptions() => opts switch {
            DeleteOptions.All => Grpc.DeleteOptions.All,
            DeleteOptions.OnlyEmptyDirectories => Grpc.DeleteOptions.OnlyEmptyDirectories,
            DeleteOptions.AllExceptDirectories => Grpc.DeleteOptions.AllExceptDirectories,
            _ => (Grpc.DeleteOptions)opts
        };
    }
    extension(Grpc.DeleteOptions opts) {
        public DeleteOptions ToOptions() => opts switch {
            Grpc.DeleteOptions.All => DeleteOptions.All,
            Grpc.DeleteOptions.OnlyEmptyDirectories => DeleteOptions.OnlyEmptyDirectories,
            Grpc.DeleteOptions.AllExceptDirectories => DeleteOptions.AllExceptDirectories,
            _ => (DeleteOptions)opts
        };
    }
    extension(IEnumerable<FsoAccess> accesses) {
        public Accesses ToGrpc() {
            var shares = new Accesses();
            shares.Items.AddRange(accesses.Select(access => access.ToGrpc()));
            return shares;
        }
    }
    extension(FsoAccess access) {
        public Access ToGrpc() {
            return new() {
                Id = access.Id.Value.ToGrpcGuid(),
                User = access.User.ToGrpcUser(),
                Fso = access.Fso.ToFsoWithType()
            };
        }
    }
    extension(Accesses accesses) {
        public IEnumerable<FsoAccess> ToFsoAccesses() {
            return accesses.Items.Select(a => new FsoAccess(
                        new(a.Id.ToGuid()),
                        a.Fso.ToFso(),
                        a.User.ToUser()
            ));
        }
    }
}
