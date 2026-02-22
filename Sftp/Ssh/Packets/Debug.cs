// Debug.cs - Part of the ZipZap project for storing files online
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

using ZipZap.Sftp.Ssh.Numbers;

namespace ZipZap.Sftp.Ssh;

internal record Debug(bool Display, string Description) : IServerPayload, IClientPayload<Debug> {
    public static Message Message => Message.Debug;

    public static bool TryParse(byte[] payload, [NotNullWhen(true)] out Debug? packet) {
        packet = null;
        var stream = new MemoryStream(payload);
        if (!(stream.SshTryReadByteSync(out var msg) && msg != (byte)Message)) return false;
        if (!stream.SshTryReadBoolSync(out var display)) return false;
        if (!stream.SshTryReadStringSync(out var description)) return false;
        if (!stream.SshTryReadStringSync(out _)) return false;
        packet = new(display, description);
        return true;
    }

    public byte[] ToPayload() {
        return new SshMessageBuilder()
             .Write((byte)Message)
             .Write(Display)
             .Write(Description)
             .Write(string.Empty)
             .Build();
    }
}
