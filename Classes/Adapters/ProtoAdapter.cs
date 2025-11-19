using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Google.Protobuf;

using ZipZap.Classes.Extensions;
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
    }
    extension(GetFsoResponse _) {
        public static async Task<GetFsoResponse> FromFileAsync(File file, Stream stream) {
            var (shared, fileData) = await file.ToRpcResponse(stream);
            return new GetFsoResponse() {
                Data = shared,
                FileData = fileData
            };
        }
        public static GetFsoResponse FromDirectory(Directory dir) {
            var (shared, dirData) = dir.ToRpcResponse();
            return new GetFsoResponse() {
                Data = shared,
                DirectoryData = dirData
            };
        }
        public static GetFsoResponse FromSymlink(Symlink symlink) {
            var (shared, linkData) = symlink.ToRpcResponse();
            return new GetFsoResponse() {
                Data = shared,
                SymlinkData = linkData
            };
        }
    }
    extension(Grpc.PathData data) {
        public PathData ToPathData(FsoId workingDirectory) => data.IdCase switch {
            Grpc.PathData.IdOneofCase.ParentId => new PathDataWithId(data.Name, new(data.ParentId.ToGuid())),
            Grpc.PathData.IdOneofCase.FilePath => new PathDataWithPath(data.Name, data.FilePath.Split('/').Where(s => !string.IsNullOrEmpty(s))),
            Grpc.PathData.IdOneofCase.None or _ => new PathDataWithId(data.Name, workingDirectory),
        };
    }
    extension(Grpc.Guid guid) {
        public Guid ToGuid() => Guid.Parse(guid.Value);
        public bool TryToGuid(out Guid result) => Guid.TryParse(guid.Value, out result);
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
