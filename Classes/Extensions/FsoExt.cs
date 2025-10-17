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
            if (fso.VirtualLocation is not null)
                data.RootId = fso.VirtualLocation.ToString();
            return data;
        }
    }
    extension(File file) {
        public async Task<FileData> ToRpcFileDataAsync(Stream stream) {
            var data = FileData.NewFileData(
                await ByteString.FromStreamAsync(stream)
            );
            return data;
        }
        public async Task<(FsoSharedData, FileData)> ToRpcResponse(Stream stream) => (file.Data.ToRpcSharedData(), await file.ToRpcFileDataAsync(stream));
    }
    extension(Directory file) {
        public DirectoryData ToRpcDirectoryData() {
            var data = DirectoryData.NewDirectoryData(
                    file.MaybeChildren
                    .SelectMany(a => a)
                    .Select(fso => new KeyValuePair<string, FsoId>(fso.Data.Name, fso.Id))
            );
            return data;
        }
        public (FsoSharedData, DirectoryData) ToRpcResponse() => (file.Data.ToRpcSharedData(), file.ToRpcDirectoryData());
    }
}
