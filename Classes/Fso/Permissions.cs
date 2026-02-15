// Permissions.cs - Part of the ZipZap project for storing files online
//     Copyright (C) 2026  Barenboim Esther wadsaek@gmail.com
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY

using System.Collections;
using System.IO;
using System.Linq;

using ZipZap.LangExt.Extensions;

using static ZipZap.LangExt.Helpers.Assertions;

namespace ZipZap.Classes;

using M = UnixFileMode;

public record struct Permissions(M Inner) {
    public readonly override string ToString() => new[] {
        (Inner & M.UserRead) != M.None ? "r" : "-",
        (Inner & M.UserWrite) != M.None ? "w" : "-",
        (Inner & M.UserExecute) == M.None
            ? (Inner & M.SetUser) != M.None
                ? "S"
                : "-"
            : (Inner & M.SetUser) != M.None
                ? "s"
                : "x",
        (Inner & M.GroupRead) != M.None ? "r" : "-",
        (Inner & M.GroupWrite) != M.None ? "w" : "-",
        (Inner & M.GroupExecute) == M.None
            ? (Inner & M.SetGroup) != M.None
                ? "S"
                : "-"
            : (Inner & M.SetGroup) != M.None
                ? "s"
                : "x",
        (Inner & M.OtherRead) != M.None ? "r" : "-",
        (Inner & M.OtherWrite) != M.None ? "w" : "-",
        (Inner & M.OtherExecute) != M.None ? "x" : "-"
    }.ConcatenateWith(string.Empty);

    public static bool TryParse(string input, out Permissions permissions) {
        permissions = new(default);
        if (input.Length != 9 && input.Length != 10) return false;
        var mode = M.None;
        if (input[0] == 'r') mode |= M.UserRead;
        else if (input[0] != '-') return false;
        if (input[1] == 'w') mode |= M.UserWrite;
        else if (input[1] != '-') return false;
        if (input[2] == 'x') mode |= M.UserExecute;
        else if (input[2] == 'S') mode |= M.SetUser;
        else if (input[2] == 's') mode |= M.SetUser | M.UserExecute;
        else if (input[2] != '-') return false;
        if (input[3] == 'r') mode |= M.GroupRead;
        else if (input[3] != '-') return false;
        if (input[4] == 'w') mode |= M.GroupWrite;
        else if (input[4] != '-') return false;
        if (input[5] == 'x') mode |= M.GroupExecute;
        else if (input[5] == 'S') mode |= M.SetGroup;
        else if (input[5] == 's') mode |= M.SetGroup | M.GroupExecute;
        else if (input[5] != '-') return false;
        if (input[6] == 'r') mode |= M.OtherRead;
        else if (input[6] != '-') return false;
        if (input[7] == 'w') mode |= M.OtherWrite;
        else if (input[7] != '-') return false;
        if (input[8] == 'x') mode |= M.OtherExecute;
        else if (input[8] != '-') return false;
        if (input.Length == 10) {
            if (input[9] == 'T') mode |= M.StickyBit;
            else return false;
        }
        permissions = new(mode);
        return true;
    }
}

public static class UnixFileModeExt {
    extension(M) {
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
            for (var i = 0; i < arr.Length; i++) {
                arr[i] = ((int)permissions.Inner >> i & 1) == 1;
            }
            return arr;
        }

        public static Permissions FileDefault => new(M.AllRead | M.UserWrite);
        public static Permissions DirectoryDefault => new(M.AllRead | M.AllExecute | M.UserWrite);
        public static Permissions SymlinkDefault => new(M.AllExecute | M.AllRead | M.AllWrite);
    }
}
