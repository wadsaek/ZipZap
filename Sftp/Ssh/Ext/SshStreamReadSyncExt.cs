// SshStreamReadSyncExt.cs - Part of the ZipZap project for storing files online
//     Copyright (C) 2026  Barenboim Esther wadsaek@gmail.com
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY, without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
//
//     You should have received a copy of the GNU General Public License
//     along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Numerics;
using System.Text;

namespace ZipZap.Sftp.Ssh;

public static class SshStreamReadSyncExt {
    extension(Stream stream) {
        public bool SshTryReadArraySync(byte[] bytes) {
            if (bytes.Length == 0) return true;
            var bytesRead = stream.Read(bytes);
            return bytesRead == bytes.Length;
        }

        public bool SshTryReadByteSync(out byte value) {
            var bytes = new byte[1];
            var success = stream.SshTryReadArraySync(bytes);

            value = success ? bytes[0] : default;
            return success;
        }
        public bool SshTryReadUint32Sync(out uint value) {
            var bytes = new byte[sizeof(uint)];
            value = default;
            var success = stream.SshTryReadArraySync(bytes);

            return success && uint.FromSsh(bytes, out value);
        }
        public bool SshTryReadInt32Sync(out int value) {
            var bytes = new byte[sizeof(int)];
            var success = stream.SshTryReadArraySync(bytes);

            value = BinaryPrimitives.ReverseEndianness(BitConverter.ToInt32(bytes));
            return success;
        }
        public bool SshTryReadUInt64Sync(out ulong value) {
            var bytes = new byte[sizeof(ulong)];
            var success = stream.SshTryReadArraySync(bytes);

            value = BinaryPrimitives.ReverseEndianness(BitConverter.ToUInt64(bytes));
            return success;
        }
        public bool SshTryReadInt64Sync(out long value) {
            var bytes = new byte[sizeof(long)];
            var success = stream.SshTryReadArraySync(bytes);

            value = BinaryPrimitives.ReverseEndianness(BitConverter.ToInt64(bytes));
            return success;
        }
        public bool SshTryReadBoolSync(out bool value) {
            var success = stream.SshTryReadByteSync(out var by);
            value = by > 0;
            return success;
        }
        public bool SshTryReadByteStringSync([NotNullWhen(true)] out byte[]? bytes) {
            bytes = null;
            if (!stream.SshTryReadUint32Sync(out var len))
                return false;

            bytes = new byte[len];
            if (!stream.SshTryReadArraySync(bytes))
                return false;

            return true;
        }
        public bool SshTryReadStringSync([NotNullWhen(true)] out string? str) {
            str = default;
            if (!stream.SshTryReadByteStringSync(out var bytes)) return false;
            try {
                str = Encoding.UTF8.GetString(bytes);
                return true;
            } catch (Exception) {
                return false;
            }
        }
        public bool SshTryReadBigIntSync(out BigInteger value) {
            value = default;
            if (!stream.SshTryReadByteStringSync(out var bytes))
                return false;
            value = new(bytes, isBigEndian: true);
            return true;
        }
        public bool SshTryReadNameListSync([NotNullWhen(true)] out NameList? nameList) {
            nameList = default;

            if (!stream.SshTryReadStringSync(out var str))
                return false;

            return NameList.TryParse(str, out nameList);
        }
    }
}

