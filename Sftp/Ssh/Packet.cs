// Packet.cs - Part of the ZipZap project for storing files online
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
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using ZipZap.Sftp.Ssh.Algorithms;

namespace ZipZap.Sftp.Ssh;

public record PacketWithoutMac(byte[] Payload, byte[] Padding) {

    public uint Length => sizeof(byte) + (uint)Payload.Length + (uint)Padding.Length;
    public byte PaddingLength => (byte)Padding.Length;
    public PacketWithoutMac(byte[] Payload) : this(Payload, []) {
        var paddingLength = 8 - ((Length + 4) % 8) + 8;
        Padding = RandomNumberGenerator.GetBytes((int)paddingLength);
    }
    public void WriteTo(byte[] buffer) {
        Debug.Assert(buffer.Length >= 4 + Length);
        if (buffer.Length < 4 + Length) throw new ArgumentException("buffer too short");
        using var stream = new MemoryStream(buffer);
        stream.SshWriteUint32Sync(Length);
        stream.SshWriteByteSync(PaddingLength);
        stream.SshWriteArraySync(Payload);
        stream.SshWriteArraySync(Padding);
    }
    public byte[] ToByteString() {
        byte[] buffer = new byte[Length + 4];
        WriteTo(buffer);
        return buffer;
    }
}
public record Packet(PacketWithoutMac Inner, byte[] Mac) {
    public uint Length => Inner.Length;
    public byte PaddingLength => Inner.PaddingLength;
    public Packet(byte[] Payload, byte[] Mac) : this(new PacketWithoutMac(Payload), Mac) { }
    public Packet(byte[] Payload, byte[] Padding, byte[] Mac) : this(new PacketWithoutMac(Payload, Padding), Mac) { }
    public byte[] ToByteString() {
        var buffer = new byte[4 + Length + Mac.Length];
        Inner.WriteTo(buffer);
        using var stream = new MemoryStream(buffer, (int)(4 + Length), Mac.Length);
        stream.SshWriteArraySync(Mac);
        return buffer;
    }
}

public static class PacketExt {
    extension(Stream stream) {
        public async Task SshWritePacket(Packet packet, CancellationToken cancellationToken) {
            var rawPacket = packet.Inner.ToByteString();
            await stream.SshWriteArray(rawPacket, cancellationToken);
            await stream.SshWriteArray(packet.Mac, cancellationToken);
        }

        public async Task<Packet?> SshTryReadPacket(IMacAlgorithm macAlgorithm, CancellationToken cancellationToken) {
            if (await stream.SshTryReadUint32(cancellationToken) is not uint length) return null;
            if (await stream.SshTryReadByte(cancellationToken) is not byte paddingLength) return null;

            var payload = new byte[(int)length - paddingLength - 1];
            await stream.SshTryReadArray(payload, cancellationToken);

            var randomPadding = new byte[paddingLength];
            await stream.SshTryReadArray(randomPadding, cancellationToken);

            var mac = new byte[macAlgorithm.Length];
            await stream.SshTryReadArray(mac, cancellationToken);
            var rawPacket = new PacketWithoutMac(payload, randomPadding);
            if (!macAlgorithm.EnsureCorrectMacFor(rawPacket, mac)) return null;
            return new Packet(payload, randomPadding, mac);
        }
    }
}
