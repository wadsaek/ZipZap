// ChannelOpen.cs - Part of the ZipZap project for storing files online
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

/// NOTE: although the server can technically send `SSH_MSG_CHANNEL_OPEN`,
/// we don't support any of the use cases
/// :33
public abstract record ChannelOpen(uint SenderChannel, uint WindowSize, uint MaxPacketSize, ChannelSpecificData SpecificData) : IClientPayload<ChannelOpen> {
    public static Message Message => Message.ChannelOpen;

    public static bool TryParse(byte[] payload, [NotNullWhen(true)] out ChannelOpen? value) {
        value = null;
        var stream = new MemoryStream(payload);
        if (!stream.ExpectMessage(Message)) return false;
        if (!stream.SshTryReadStringSync(out var channelType)) return false;
        if (!stream.SshTryReadUint32Sync(out var senderChannel)) return false;
        if (!stream.SshTryReadUint32Sync(out var windowSize)) return false;
        if (!stream.SshTryReadUint32Sync(out var maxPacketSize)) return false;
        switch (channelType) {
            case ChannelSpecificData.Session.ChannelType: {
                    value = new ChannelOpenGen<ChannelSpecificData.Session>(
                        senderChannel,
                        windowSize,
                        maxPacketSize,
                        new()
                    );
                    break;
                }
            default: {
                    value = new ChannelOpenGen<ChannelSpecificData.UnrecognizedData>(
                        senderChannel,
                        windowSize,
                        maxPacketSize,
                        new(
                            channelType,
                            payload[(int)stream.Position..]
                        )
                    );
                    break;
                }
        }
        return true;
    }
    public sealed record ChannelOpenGen<T>(uint SenderChannel, uint WindowSize, uint MaxPacketSize, T SpecificDataGen)
        : ChannelOpen(SenderChannel, WindowSize, MaxPacketSize, SpecificDataGen)
        where T : ChannelSpecificData;
}
