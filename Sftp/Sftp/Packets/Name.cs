// Name.cs - Part of the ZipZap project for storing files online
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

using System.Collections.Immutable;
using System.Linq;

using ZipZap.Sftp.Sftp.Numbers;
using ZipZap.Sftp.Ssh;

namespace ZipZap.Sftp.Sftp;

internal record Name(uint Id, ImmutableList<FileName> Names) : ISftpServerPayload {
    public Message PacketType => Message.Name;

    public Packet ToPacket() {
        return new SshMessageBuilder()
           .Write(PacketType)
           .Write(Id)
           .Write((uint)Names.Count)
           .WriteArray(Names.SelectMany(n => n.ToBytes()).ToArray())
           .Build();
    }
}

