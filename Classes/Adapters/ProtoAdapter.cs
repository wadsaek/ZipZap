using System.Collections.Generic;
using System.Linq;

using Google.Protobuf;

using ZipZap.Grpc;
namespace ZipZap.Classes.Adapters;


public static class ProtoAdapter {
    extension(FileData data) {
        public static FileData NewFileData(ByteString content) =>
            new() {
                Content = content
            };
    }
    extension(DirectoryData data) {
        public static DirectoryData NewDirectoryData(IEnumerable<KeyValuePair<string, FsoId>> map) {
            var a = new DirectoryData();
            a.Entries.Add(
                map
               .Select(f => new KeyValuePair<string, string>(f.Key, f.Value.Value.ToString()))
               .ToDictionary());
            return a;
        }
    }
    extension(SymlinkData data) {
        public static SymlinkData NewSymlinkData(string target) =>
            new() {
                Target = target
            };
    }
}
