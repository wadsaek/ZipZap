using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Google.Protobuf;

using ZipZap.Classes.Adapters;
using ZipZap.Classes.Helpers;
using ZipZap.Grpc;
namespace ZipZap.Classes.Extensions;

public static class FsoExt {
    extension(FsData fso) {
        public FsoSharedData ToRpcSharedData() {
            var data = new FsoSharedData {
                Group = fso.FsoGroup,
                Name = fso.Name,
                Owner = fso.FsoOwner,
                Permissions = (int)fso.Permissions.Inner,
            };
            if (fso.VirtualLocation is Some<MaybeEntity<Directory, FsoId>>(var location))
                data.RootId = location.Id.Value.ToGrpcGuid();
            return data;
        }
    }
    extension(File file) {
        public static async Task<FileData> ToRpcFileDataAsync(Stream stream) {
            var data = FileData.NewFileData(
                await ByteString.FromStreamAsync(stream)
            );
            return data;
        }
        public async Task<(FsoSharedData, FileData)> ToRpcResponse(Stream stream) => (file.Data.ToRpcSharedData(), await File.ToRpcFileDataAsync(stream));
    }
    extension(Directory dir) {
        public DirectoryData ToRpcDirectoryData() {
            var data = new DirectoryData();
            data.Entries.Add(
                dir.MaybeChildren.UnwrapOr([])
               .Select(f => new KeyValuePair<string, Guid>(f.Data.Name, f.Id.Value.ToGrpcGuid()))
               .ToDictionary());
            return data;
        }
        public (FsoSharedData, DirectoryData) ToRpcResponse() => (dir.Data.ToRpcSharedData(), dir.ToRpcDirectoryData());
    }
    extension(Symlink link) {
        public SymlinkData ToRpcLinkData() {
            var data = SymlinkData.NewSymlinkData(
                    link.Target
            );
            return data;
        }
        public (FsoSharedData, SymlinkData) ToRpcResponse() => (link.Data.ToRpcSharedData(), link.ToRpcLinkData());
    }
}
