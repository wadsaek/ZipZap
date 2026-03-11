// Init.cs - Part of the ZipZap project for storing files online
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

// the SSH_FXP_INIT packet can actually have extensions, but we don't support that
// (neither does openssh so we're good there)
// also SSH_FXP_INIT doesn't support extensions in later sftp versions
public record Init : ISftpClientPayload<Init> {
    public uint Version => 3;
    public static Message PacketType => Message.Init;

    public static bool TryParse(byte[] bytes, [NotNullWhen(true)] out Init? value) {
        value = null;
        var stream = new MemoryStream(bytes);
        if (!stream.ExpectMessage(PacketType)) return false;
        value = new();
        return true;
    }
}
