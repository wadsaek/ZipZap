// SshStreamWriteSyncExt.cs - Part of the ZipZap project for storing files online
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
using System.IO;
using System.Numerics;
using System.Text;

namespace ZipZap.Sftp.Ssh;

public static class SshStreamWriteSyncExt {
    extension(Stream stream) {
        public void SshWriteArraySync(ReadOnlySpan<byte> bytes)
            => stream.Write(bytes);

        public void SshWriteByteSync(byte b)
            => stream.SshWriteArraySync(new[] { b });

        public void SshWriteBoolSync(bool b)
            => stream.SshWriteByteSync(b ? (byte)1 : (byte)0);

        public void SshWriteByteStringSync(byte[] bytes) {
            stream.SshWriteUint32Sync((uint)bytes.Length);
            stream.SshWriteArraySync(bytes);
        }
        public void SshWriteStringSync(string str) {
            var bytes = Encoding.UTF8.GetBytes(str);
            stream.SshWriteByteStringSync(bytes);
        }
        public void SshWriteBigIntSync(BigInteger bigint) {
            stream.SshWriteByteStringSync(bigint == 0 ? [] : bigint.ToByteArray(isBigEndian: true));
        }

        public void SshWriteUint32Sync(uint n) {
            var reversed = BinaryPrimitives.ReverseEndianness(n);
            var bytes = BitConverter.GetBytes(reversed);
            stream.SshWriteArraySync(bytes);
        }
        public void SshWriteInt32Sync(int n) {
            var reversed = BinaryPrimitives.ReverseEndianness(n);
            var bytes = BitConverter.GetBytes(reversed);
            stream.SshWriteArraySync(bytes);
        }
        public void SshWriteUint64Sync(ulong n) {
            var reversed = BinaryPrimitives.ReverseEndianness(n);
            var bytes = BitConverter.GetBytes(reversed);
            stream.SshWriteArraySync(bytes);
        }
        public void SshWriteInt64Sync(long n) {
            var reversed = BinaryPrimitives.ReverseEndianness(n);
            var bytes = BitConverter.GetBytes(reversed);
            stream.SshWriteArraySync(bytes);
        }

        public void SshWriteNameListSync(NameList names) {
            var str = names.ToString();
            var bytes = new byte[str.Length];
            if (Encoding.ASCII.TryGetBytes(str, bytes, out _))
                stream.SshWriteByteStringSync(bytes);
        }
    }
}

