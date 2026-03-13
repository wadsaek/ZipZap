// ChannelExtendedData.cs - Part of the ZipZap project for storing files online
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

sealed record ChannelExtendedData(uint Recipient, ChannelExtendedDataType DataType, byte[] Bytes) : IServerPayload, IClientPayload<ChannelExtendedData>, IChannelPayload {
    public static Message Message => Message.ChannelExtendedData;

    public uint ChannelWindowLength => 4 + 4 + (uint)Bytes.Length;

    public static bool TryParse(byte[] payload, [NotNullWhen(true)] out ChannelExtendedData? value) {
        value = null;
        var stream = new MemoryStream(payload);
        if (!stream.ExpectMessage(Message)) return false;
        if (!stream.SshTryReadUint32Sync(out var recipient)) return false;
        if (!stream.SshTryReadUint32Sync(out var type)) return false;
        if (!stream.SshTryReadByteStringSync(out var bytes)) return false;
        value = new(recipient, (ChannelExtendedDataType)type, bytes);
        return true;
    }

    public byte[] ToPayload() {
        return new SshMessageBuilder()
            .Write(Message)
            .Write(Recipient)
            .Write((uint)DataType)
            .WriteByteString(Bytes)
            .Build();
    }
}
