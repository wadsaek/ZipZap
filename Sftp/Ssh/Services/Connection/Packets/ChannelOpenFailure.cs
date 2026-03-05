// ChannelOpenFailure.cs - Part of the ZipZap project for storing files online
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

// byte      SSH_MSG_CHANNEL_OPEN_FAILURE
// uint32    recipient channel
// uint32    reason code
// string    description in ISO-10646 UTF-8 encoding [RFC3629]
// string    language tag [RFC3066]
public sealed record ChannelOpenFailure(uint Recipient, ChannelOpenFailureCode ReasonCode, string Description) : IServerPayload, IClientPayload<ChannelOpenFailure> {
    public static Message Message => Message.ChannelOpenFailure;
    public static bool TryParse(byte[] payload, [NotNullWhen(true)] out ChannelOpenFailure? value) {
        value = null;
        var stream = new MemoryStream(payload);
        if (!stream.ExpectMessage(Message)) return false;
        if (!stream.SshTryReadUint32Sync(out var recipient)) return false;
        if (!stream.SshTryReadUint32Sync(out var reasonRaw)) return false;
        if (!stream.SshTryReadStringSync(out var description)) return false;
        if (!stream.SshTryReadStringSync(out _)) return false;
        value = new(recipient, (ChannelOpenFailureCode)reasonRaw, description);
        return true;
    }

    public byte[] ToPayload() {
        return new SshMessageBuilder()
            .Write(Message)
            .Write(Recipient)
            .Write((uint)ReasonCode)
            .Write(Description)
            .Write(string.Empty)
            .Build();
    }
}
