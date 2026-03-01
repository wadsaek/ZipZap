// SshStreamWriteExt.cs - Part of the ZipZap project for storing files online
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
using System.Threading;
using System.Threading.Tasks;

namespace ZipZap.Sftp.Ssh;

public static class SshStreamWriteExt {
    extension(Stream stream) {
        public async Task SshWriteArray(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
            => await stream.WriteAsync(bytes, cancellationToken);

        public async Task SshWriteByte(byte b, CancellationToken cancellationToken)
            => await stream.SshWriteArray(new[] { b }, cancellationToken);

        public async Task SshWriteBool(bool b, CancellationToken cancellationToken)
            => await stream.SshWriteByte(b ? (byte)1 : (byte)0, cancellationToken);

        public async Task SshWriteByteString(byte[] bytes, CancellationToken cancellationToken) {
            await stream.SshWriteUint32((uint)bytes.Length, cancellationToken);
            await stream.SshWriteArray(bytes, cancellationToken);
        }
        public async Task SshWriteString(string str, CancellationToken cancellationToken) {
            var bytes = Encoding.UTF8.GetBytes(str);
            await stream.SshWriteByteString(bytes, cancellationToken);
        }
        public async Task SshWriteBigInt(BigInteger bigint, CancellationToken cancellationToken) {
            await stream.SshWriteByteString(bigint == 0 ? [] : bigint.ToByteArray(isBigEndian: true), cancellationToken);
        }

        public async Task SshWriteUint32(uint n, CancellationToken cancellationToken) {
            var reversed = BinaryPrimitives.ReverseEndianness(n);
            var bytes = BitConverter.GetBytes(reversed);
            await stream.SshWriteArray(bytes, cancellationToken);
        }
        public async Task SshWriteInt32(int n, CancellationToken cancellationToken) {
            var reversed = BinaryPrimitives.ReverseEndianness(n);
            var bytes = BitConverter.GetBytes(reversed);
            await stream.SshWriteArray(bytes, cancellationToken);
        }
        public async Task SshWriteUint64(ulong n, CancellationToken cancellationToken) {
            var reversed = BinaryPrimitives.ReverseEndianness(n);
            var bytes = BitConverter.GetBytes(reversed);
            await stream.SshWriteArray(bytes, cancellationToken);
        }
        public async Task SshWriteInt64(long n, CancellationToken cancellationToken) {
            var reversed = BinaryPrimitives.ReverseEndianness(n);
            var bytes = BitConverter.GetBytes(reversed);
            await stream.SshWriteArray(bytes, cancellationToken);
        }

        public async Task SshWriteNameList(NameList names, CancellationToken cancellationToken) {
            var str = names.ToString();
            var bytes = new byte[str.Length];
            if (Encoding.ASCII.TryGetBytes(str, bytes, out _))
                await stream.SshWriteByteString(bytes, cancellationToken);
        }
    }

}
