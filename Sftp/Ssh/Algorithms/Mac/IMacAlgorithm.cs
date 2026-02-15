// IMacAlgorithm.cs - Part of the ZipZap project for storing files online
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

public interface IMacAlgorithm : INamed {
    public int Length { get; }

    public bool EnsureCorrectMacFor(PacketWithoutMac packet, byte[] mac);

    public Task<byte[]> GenerateMacForAsync(uint sequencial, BigInteger secret, byte[] bytes, CancellationToken cancellationToken);
}
public class NoMacAlgorithm : IMacAlgorithm {
    public int Length { get; }

    public NameList.Item Name => new NameList.GlobalName("none");

    public bool EnsureCorrectMacFor(PacketWithoutMac packet, byte[] mac) => mac is [];

    public Task<byte[]> GenerateMacForAsync(uint _sshState, BigInteger _key, byte[] _bytes, CancellationToken _token) => Task.FromResult<byte[]>([]);
}

public class HMacSha2256EtmOpenSsh : IMacAlgorithm {
    public int Length => 256;

    public NameList.Item Name => new NameList.LocalName("hmac-sha2-256-etm", "openssh.com");

    // TODO: implement
    public bool EnsureCorrectMacFor(PacketWithoutMac packet, byte[] mac) {
        return true;
    }

    public async Task<byte[]> GenerateMacForAsync(uint sshState, BigInteger secret, byte[] bytes, CancellationToken cancellationToken) {
        var buffer = new byte[sizeof(uint) + bytes.Length];
        await using var stream = new MemoryStream(buffer);
        stream.SshWriteUint32Sync(sshState);
        stream.SshWriteArraySync(bytes);
        return await HMACSHA256.HashDataAsync(secret.ToByteArray(), stream, cancellationToken);
    }
}
