// GlobalRequest.cs - Part of the ZipZap project for storing files online
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

namespace ZipZap.Sftp.Ssh.Services.Connection.Packets;

// NOTE: this is a more complex packet, but we don't support most of its
// intricasies. this simple implementation should work wonders.
public record GlobalRequest(bool WantReply) : IClientPayload<GlobalRequest> {
    public static Message Message => Message.Newkeys;

    public static bool TryParse(byte[] payload, [NotNullWhen(true)] out GlobalRequest? value) {
        value = null;
        var stream = new MemoryStream(payload);
        if (!stream.ExpectMessage(Message)) return false;
        if (!stream.SshTryReadStringSync(out _)) return false;
        if (!stream.SshTryReadBoolSync(out var wantReply)) return false;
        value = new(wantReply);
        return true;

    }

}
