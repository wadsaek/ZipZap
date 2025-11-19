using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Google.Protobuf;

using ZipZap.Classes.Extensions;
using ZipZap.Classes.Helpers;
using ZipZap.Grpc;

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
                SymlinkData = new() { Target = target },
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
            return new GetFsoResponse() {
                FsoId = file.Id.Value.ToGrpcGuid(),
                Data = shared,
                FileData = fileData
            };
        }
        public static GetFsoResponse FromDirectory(Directory dir) {
            var (shared, dirData) = dir.ToRpcResponse();
            return new GetFsoResponse() {
                FsoId = dir.Id.Value.ToGrpcGuid(),
                Data = shared,
                DirectoryData = dirData
            };
        }
        public static GetFsoResponse FromSymlink(Symlink symlink) {
            var (shared, linkData) = symlink.ToRpcResponse();
            return new GetFsoResponse() {
                FsoId = symlink.Id.Value.ToGrpcGuid(),
                Data = shared,
                SymlinkData = linkData
            };
        }
        public Fso ToFso() {
            var id = new FsoId(response.FsoId.ToGuid());
            var data = response.Data.ToFsData();
            return response.SpecificDataCase switch {
                GetFsoResponse.SpecificDataOneofCase.FileData => new File(id, data),
                GetFsoResponse.SpecificDataOneofCase.SymlinkData => new Symlink(id, data, response.SymlinkData.Target),
                GetFsoResponse.SpecificDataOneofCase.DirectoryData => new Directory(id, data) {
                    MaybeChildren = response.DirectoryData.ToFsos().ToOption()
                },
                GetFsoResponse.SpecificDataOneofCase.None or _ => throw new InvalidEnumArgumentException(nameof(response.SpecificDataCase))

            };
        }
    }
    extension(GetRootResponse response) {
        public Directory ToDirectory() => new(
                response.FsoId.ToGuid().ToFsoId(),
                response.Data.ToFsData()
                ) {
            MaybeChildren = response.DirectoryData.ToFsos().ToOption()
        };
    }
    extension(Grpc.PathData data) {
        public PathData ToPathData(FsoId workingDirectory) => data.IdCase switch {
            Grpc.PathData.IdOneofCase.FilePath => new PathDataWithPath(data.Name, data.FilePath.Split('/').Where(s => !string.IsNullOrEmpty(s))),
            Grpc.PathData.IdOneofCase.ParentId => new PathDataWithId(data.Name, data.ParentId.ToGuid().ToFsoId()),
            Grpc.PathData.IdOneofCase.None or _ => new PathDataWithId(data.Name, workingDirectory),
        };
    }
    extension(PathData pathData) {
        public Grpc.PathData ToRpcPathData() {

            var grpcPathData = new Grpc.PathData() {
                Name = pathData.Name,
            };
            if (pathData is PathDataWithPath { Path: var path }) {
                var pathList = path.ToList();
                if (pathList.Count != 0)
                    grpcPathData.FilePath = pathList.ConcatenateWith("/");
            }
            if (pathData is PathDataWithId { ParentId: var id })
                grpcPathData.ParentId = id.Value.ToGrpcGuid();
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
    extension(Grpc.User user) {
        public User ToUser() => new(
            user.Id.ToGuid().ToUserId(),
            user.Username,
            [],
            user.Email,
            user.RootId.ToGuid().ToFsoId().AsIdOf<Directory>()
        );
    }
    extension(User user) {
        public Grpc.User ToGrpcUser() => new() {
            RootId = user.Root.Id.Value.ToGrpcGuid(),
            Id = user.Id.Value.ToGrpcGuid(),
            Email = user.Email,
            Username = user.Username
        };
    }
}
