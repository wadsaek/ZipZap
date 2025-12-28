using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Google.Protobuf;

using ZipZap.Classes.Adapters;
using ZipZap.Grpc;
namespace ZipZap.Classes.Extensions;

public static class FsoExt {
    extension(Fso fso) {
        public FsoSharedData ToRpcSharedData() => new() {
            Group = fso.Data.FsoGroup,
            Name = fso.Data.Name,
            Owner = fso.Data.FsoOwner,
            Permissions = (int)fso.Data.Permissions.Inner,
            RootId = (fso.Data.VirtualLocation
                ?.Id
                ?? fso.Id)
                .Value.ToGrpcGuid()
        };
    }
    extension(File file) {
        public static async Task<FileData> ToRpcFileDataAsync(Stream stream) {
            var data = FileData.NewFileData(
                await ByteString.FromStreamAsync(stream)
            );
            return data;
        }
        public async Task<(FsoSharedData, FileData)> ToRpcResponse(Stream stream) => (file.ToRpcSharedData(), await File.ToRpcFileDataAsync(stream));
    }
    extension(Directory dir) {
        public DirectoryData ToRpcDirectoryData() {
            var data = new DirectoryData();
            data.Entries.Add(
                dir.MaybeChildren
                    .Select(fso => fso.ToFsoWithType())
           );
            return data;
        }
        public (FsoSharedData, DirectoryData) ToRpcResponse() => (dir.ToRpcSharedData(), dir.ToRpcDirectoryData());
    }
    extension(Symlink link) {
        public SymlinkData ToRpcLinkData() {
            var data = SymlinkData.NewSymlinkData(
                    link.Target
            );
            return data;
        }
        public (FsoSharedData, SymlinkData) ToRpcResponse() => (link.ToRpcSharedData(), link.ToRpcLinkData());
    }
}
