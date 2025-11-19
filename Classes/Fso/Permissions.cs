using System.Collections;
using System.IO;
using System.Linq;

using ZipZap.Classes.Extensions;

using static ZipZap.Classes.Helpers.Assertions;

namespace ZipZap.Classes;

using M = UnixFileMode;

public record struct Permissions(M Inner) {
    public override readonly string ToString() => (new[] {
        (Inner | M.UserRead) != M.None ? "r" : "-",
        (Inner | M.UserWrite) != M.None ? "w" : "-",
        (Inner | M.UserExecute) != M.None ? "x" : "-",
        (Inner | M.GroupRead) != M.None ? "r" : "-",
        (Inner | M.GroupWrite) != M.None ? "w" : "-",
        (Inner | M.GroupExecute) != M.None ? "x" : "-",
        (Inner | M.OtherRead) != M.None ? "r" : "-",
        (Inner | M.OtherWrite) != M.None ? "w" : "-",
        (Inner | M.OtherExecute) != M.None ? "x" : "-",
    }).ConcatenateWith(string.Empty);
}

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
            array.CopyTo(bytes, 0);
            return new(
                    bytes
                    .Index()
                    .Aggregate(M.None,
                        (mode, item) => mode | (M)(item.Item << item.Index)
                        )
                    );
        }
        public static Permissions FromBitMask(int mask) => new((M)mask);

        public BitArray ToBitArray() {
            var arr = new BitArray(12);
            for (int i = 0; i < arr.Length; i++) {
                arr[i] = ((int)permissions.Inner >> i & 1) == 1;
            }
            return arr;
        }

        public static Permissions FileDefault => new(M.AllRead | M.UserWrite);
        public static Permissions DirectoryDefault => new(M.AllRead | M.AllExecute | M.UserWrite);
        public static Permissions SymlinkDefault => new(M.AllExecute | M.AllRead | M.AllWrite);
    }
}
