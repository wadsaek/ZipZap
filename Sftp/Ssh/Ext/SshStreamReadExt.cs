// SshStreamReadExt.cs - Part of the ZipZap project for storing files online
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
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ZipZap.Sftp.Ssh;

public static class SshStreamReadExt {
    extension(Stream stream) {
        public async Task<bool> SshTryReadArray(byte[] bytes, CancellationToken cancellationToken) {
            if (bytes.Length == 0) return true;
            try {
                await stream.ReadExactlyAsync(
                        bytes,
                        0, bytes.Length,
                        cancellationToken
                );
                return true;
            } catch (EndOfStreamException) {
                return false;
            }
        }

        public async Task<byte?> SshTryReadByte(CancellationToken cancellationToken) {
            var bytes = new byte[1];
            var success = await stream.SshTryReadArray(bytes, cancellationToken);

            return success ? bytes[0] : null;
        }
        public async Task<uint?> SshTryReadUint32(CancellationToken cancellationToken) {
            var bytes = new byte[sizeof(uint)];
            var success = await stream.SshTryReadArray(bytes, cancellationToken);

            return success
                ? BinaryPrimitives.ReverseEndianness(BitConverter.ToUInt32(bytes))
                : null;
        }
        public async Task<int?> SshTryReadInt32(CancellationToken cancellationToken) {
            var bytes = new byte[sizeof(int)];
            var success = await stream.SshTryReadArray(bytes, cancellationToken);

            return success
                ? BinaryPrimitives.ReverseEndianness(BitConverter.ToInt32(bytes))
                : null;
        }
        public async Task<ulong?> SshTryReadUInt64(CancellationToken cancellationToken) {
            var bytes = new byte[sizeof(ulong)];
            var success = await stream.SshTryReadArray(bytes, cancellationToken);

            return success
                ? BinaryPrimitives.ReverseEndianness(BitConverter.ToUInt64(bytes))
                : null;
        }
        public async Task<long?> SshTryReadInt64(CancellationToken cancellationToken) {
            var bytes = new byte[sizeof(long)];
            var success = await stream.SshTryReadArray(bytes, cancellationToken);

            return success
                ? BinaryPrimitives.ReverseEndianness(BitConverter.ToInt64(bytes))
                : null;
        }
        public async Task<bool?> SshTryReadBool(CancellationToken cancellationToken) {
            return await stream.SshTryReadByte(cancellationToken) switch {
                null => null,
                0 => false,
                _ => true
            };
        }
        public async Task<byte[]?> SshTryReadByteString(CancellationToken cancellationToken) {
            var lenWrapped = await stream.SshTryReadUint32(cancellationToken);
            if (lenWrapped is not uint len)
                return null;

            var bytes = new byte[len];
            if (!await stream.SshTryReadArray(bytes, cancellationToken))
                return null;
            return bytes;
        }
        public async Task<string?> SshTryReadString(CancellationToken cancellationToken) {
            var bytes = await stream.SshTryReadByteString(cancellationToken);
            if (bytes is null)
                return null;
            try {
                return Encoding.UTF8.GetString(bytes);
            } catch (Exception) {
                return null;
            }
        }
        public async Task<BigInteger?> SshTryReadBigInt(CancellationToken cancellationToken) {
            var bytes = await stream.SshTryReadByteString(cancellationToken);
            if (bytes is null)
                return null;
            return new(bytes, isBigEndian: true);
        }
        public async Task<NameList?> SshTryReadNameList(CancellationToken cancellationToken) {
            var str = await stream.SshTryReadString(cancellationToken);
            if (str is null)
                return null;
            if (str is "")
                return new([]);
            var maybeNames = str.Split(',');
            var names = new List<NameList.Item>(maybeNames.Length);
            foreach (var name in maybeNames) {
                if (!NameList.Item.TryParse(name, out var item))
                    return null;
                names.Add(item);
            }
            return new(names.ToArray());
        }
    }
}

