// SftpExtension.cs - Part of the ZipZap project for storing files online
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

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ZipZap.Sftp.Sftp;

public abstract record SftpExtension(string Name, string Version) {
    internal static bool TryParse(string extname, MemoryStream stream, out SftpExtension ext) {
        throw new NotImplementedException();
    }
    public abstract Task HandlePacket(byte[] payload, ISftpRequestHandler handler, CancellationToken cancellationToken);

    public sealed record LSetStat() : SftpExtension("lsetstat@openssh.com", "1") {
        public override Task HandlePacket(byte[] payload, ISftpRequestHandler handler, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
    }
}

