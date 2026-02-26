// Ignore.cs - Part of the ZipZap project for storing files online
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

using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Security.Cryptography;

using ZipZap.Sftp.Ssh.Numbers;

namespace ZipZap.Sftp.Ssh;

internal record Ignore(byte[] Data) : IServerPayload, IClientPayload<Ignore> {
    public static Message Message => Message.Ignore;

    public static Ignore Random() {
        var size = RandomNumberGenerator.GetInt32(1000);
        var data = RandomNumberGenerator.GetBytes(size);
        return new(data);
    }

    public static bool TryParse(byte[] payload, [NotNullWhen(true)] out Ignore? packet) {
        packet = null;
        var stream = new MemoryStream(payload);
        if (!(stream.SshTryReadByteSync(out var msg) && msg != (byte)Message)) return false;
        if (!stream.SshTryReadByteStringSync(out var data)) return false;
        packet = new(data);
        return true;
    }

    public byte[] ToPayload() {
        return new SshMessageBuilder()
             .Write((byte)Message)
             .WriteByteString(Data)
             .Build();
    }
}
