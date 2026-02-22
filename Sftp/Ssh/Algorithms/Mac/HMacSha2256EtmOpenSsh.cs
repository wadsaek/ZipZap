// HMacSha2256EtmOpenSsh.cs - Part of the ZipZap project for storing files online
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

using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace ZipZap.Sftp.Ssh.Algorithms;

public class HMacSha2256EtmOpenSsh : IMacAlgorithm {
    public int Length => 256;

    public NameList.Item Name => new NameList.LocalName("hmac-sha2-256-etm", "openssh.com");

    public bool IsEncryptThenMac => true;

    public int KeyLength => throw new System.NotImplementedException();

    public IMacGenerator CreateGenerator(uint sequential, byte[] IntegrityKey) {
        throw new System.NotImplementedException();
    }

    public IMacValidator CreateValidator(uint sequential, byte[] IntegrityKey) {
        throw new System.NotImplementedException();
    }

    public async Task<byte[]> GenerateMacForAsync(uint sequential, BigInteger secret, byte[] bytes, CancellationToken cancellationToken) {
        var buffer = new byte[sizeof(uint) + bytes.Length];
        await using var stream = new MemoryStream(buffer);
        stream.SshWriteUint32Sync(sequential);
        stream.SshWriteArraySync(bytes);
        return await HMACSHA256.HashDataAsync(secret.ToByteArray(), stream, cancellationToken);
    }
}

