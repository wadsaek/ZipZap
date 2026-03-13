// Extended.cs - Part of the ZipZap project for storing files online
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

using ZipZap.Sftp.Sftp.Numbers;
using ZipZap.Sftp.Ssh;

namespace ZipZap.Sftp.Sftp;

public record Extended(uint Id, string Name, SftpExtension Extension) : ISftpClientPayload<Extended> {
    public static Message PacketType => Message.Extended;

    public static bool TryParse(byte[] bytes, [NotNullWhen(true)] out Extended? value) {
        value = null;
        var stream = new MemoryStream(bytes);
        if (!stream.ExpectMessage(PacketType)) return false;
        if (!stream.SshTryReadUint32Sync(out var id)) return false;
        if (!stream.SshTryReadStringSync(out var name)) return false;
        if (!SftpExtension.TryParse(name, stream, out var extension)) return false;
        value = new(id, name, extension);
        return true;
    }
}
