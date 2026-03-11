// ISshChannel.cs - Part of the ZipZap project for storing files online
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

using System.Threading;
using System.Threading.Tasks;

using ZipZap.Sftp.Ssh.Services.Connection.Packets;

namespace ZipZap.Sftp.Ssh.Services.Connection;

internal interface ISshChannel {
    public ClosedStatus Status { get; }
    public uint PeerId { get; }
    public uint WindowSizeCtS { get; }
    public uint WindowSizeStC { get; }
    public uint PacketSizeCtS { get; }
    public uint PacketSizeStC { get; }

    public Task AdjustWindow(uint bytesToAdd, CancellationToken cancellationToken);
    public Task SendData(ChannelData payload, CancellationToken cancellationToken);
    public Task HandleExtendedData(ChannelExtendedData data, CancellationToken cancellationToken);
    public Task RegisterEof(CancellationToken cancellationToken);
    public Task Close(CancellationToken cancellationToken);
    public Task HandleRequest(ChannelRequest request, CancellationToken cancellationToken);

}

