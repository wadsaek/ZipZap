using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Google.Protobuf;

using ZipZap.Classes.Extensions;
using ZipZap.Grpc;

using DataCase = ZipZap.Grpc.GetFsoResponse.SpecificDataOneofCase;
using IdCase = ZipZap.Grpc.PathData.IdOneofCase;
using Guid = System.Guid;

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
            switch (pathData)
            {
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
                    fsoSharedData.Owner,
                    fsoSharedData.Group
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
            user.RootId.ToGuid().ToFsoId().AsIdOf<Directory>()
        );
    }
    extension(User user) {
        public Grpc.User ToGrpcUser() => new() {
            RootId = user.Root.Id.Value.ToGrpcGuid(),
            Id = user.Id.Value.ToGrpcGuid(),
            Email = user.Email,
            Username = user.Username,
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
}
