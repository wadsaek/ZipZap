using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Google.Protobuf;

using ZipZap.Classes.Extensions;
using ZipZap.Grpc;

using Guid = System.Guid;
namespace ZipZap.Classes.Adapters;


public static class ProtoAdapter {
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
    extension(DirectoryData _) {
        public static DirectoryData NewDirectoryData(IDictionary<string, Guid> dict) {
            var data = new DirectoryData();
            data.Entries.Add(dict
                    .Select(pair => KeyValuePair.Create(pair.Key, new Grpc.Guid { Value = pair.Value.ToString() }))
                    .ToDictionary());
            return data;
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
            Grpc.PathData.IdOneofCase.FilePath => new PathDataWithPath(data.Name, data.FilePath.Split('/').Where(string.IsNullOrEmpty)),
            Grpc.PathData.IdOneofCase.ParentId => new PathDataWithId(data.Name, new(data.ParentId.ToGuid())),
            Grpc.PathData.IdOneofCase.None or _ => new PathDataWithId(data.Name, workingDirectory),
        };
    }
    extension(Grpc.Guid guid) {
        public Guid ToGuid() => Guid.Parse(guid.Value);
        public bool TryToGuid(out Guid result) => Guid.TryParse(guid.Value, out result);
    }
}
