// ChannelOpenConfirmation.cs - Part of the ZipZap project for storing files online
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

using ZipZap.Sftp.Ssh.Numbers;

namespace ZipZap.Sftp.Ssh.Services.Connection.Packets;

// byte      SSH_MSG_CHANNEL_OPEN_CONFIRMATION
// uint32    recipient channel
// uint32    sender channel
// uint32    initial window size
// uint32    maximum packet size
public record ChannelOpenConfirmation<T>(uint Recipient, uint Sender, uint WindowSize, uint MaxPacketSize, T SpecificData)
: IServerPayload
where T : ChannelSpecificData {
    public static Message Message => Message.ChannelOpenConfirmation;

    public byte[] ToPayload() {
        return new SshMessageBuilder()
            .Write(Message)
            .Write(Recipient)
            .Write(Sender)
            .Write(WindowSize)
            .Write(MaxPacketSize)
            .WriteArray(SpecificData.ToPayload())
            .Build();
    }
}
