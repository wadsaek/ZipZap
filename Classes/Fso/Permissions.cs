using System.Collections;
using System.IO;
using System.Linq;

using static ZipZap.Classes.Helpers.Assertions;

namespace ZipZap.Classes;

using M = UnixFileMode;

public record struct Permissions(M Inner);

public static class UnixFileModeExt {
    extension(M mode) {
        public static M AllRead => M.UserRead | M.GroupRead | M.OtherRead;
        public static M AllWrite => M.UserWrite | M.GroupWrite | M.OtherWrite;
        public static M AllExecute => M.UserExecute | M.GroupExecute | M.OtherExecute;
    }
}

public static class PermissionsExt {
    extension(Permissions permissions) {
        public static Permissions FromBitArray(BitArray array) {
            Assert(array.Length == 12);
            var bytes = new byte[array.Length];
            array.CopyTo(bytes,0);
            return new(
                    bytes
                    .Index()
                    .Aggregate(M.None,
                        (mode, item) => mode | (M)(item.Item << item.Index)
                        )
                    );
        }

        public BitArray ToBitArray() {
            var arr = new BitArray(12);
            for (int i = 0; i < arr.Length; i++) {
                arr[i] = ((int)permissions.Inner >> i & 1) == 1;
            }
            return arr;
        }

        public static Permissions FileDefault => new(M.AllRead | M.UserWrite);
        public static Permissions GroupDefault => new(M.AllRead | M.AllExecute | M.UserWrite);
        public static Permissions SymlinkDefault => new(M.AllExecute | M.AllRead | M.AllWrite);
    }
}
