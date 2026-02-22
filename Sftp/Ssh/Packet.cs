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
using System.IO;
using System.Security.Cryptography;

namespace ZipZap.Sftp.Ssh;

public record Packet(byte[] Payload, byte[] Padding) : IToByteString {

    public uint Length => sizeof(byte) + (uint)Payload.Length + (uint)Padding.Length;
    public byte PaddingLength => (byte)Padding.Length;

    public uint BufferLength => Length + 4;

    public Packet(byte[] payload, uint alignment, int offset = 0) : this(payload, []) {
        var paddingLength = 2 * alignment - ((BufferLength - offset) % alignment);
        Padding = RandomNumberGenerator.GetBytes((int)paddingLength);
    }

    public void WriteTo(byte[] buffer) {
        System.Diagnostics.Debug.Assert(buffer.Length >= BufferLength);
        if (buffer.Length < 4 + Length) throw new ArgumentException("buffer too short");
        using var stream = new MemoryStream(buffer);
        stream.SshWriteUint32Sync(Length);
        stream.SshWriteByteSync(PaddingLength);
        stream.SshWriteArraySync(Payload);
        stream.SshWriteArraySync(Padding);
    }

    public byte[] ToByteString() {
        byte[] buffer = new byte[BufferLength];
        WriteTo(buffer);
        return buffer;
    }
}

