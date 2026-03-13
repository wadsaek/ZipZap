// Open.cs - Part of the ZipZap project for storing files online
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

public record Open(uint Id, string Filename, OpenFlags Flags, FileAttributes Attrs) : ISftpClientPayload<Open> {
   public static Message PacketType => Message.Open;

   public static bool TryParse(byte[] bytes, [NotNullWhen(true)] out Open? value) {
      value = null;
      var stream = new MemoryStream(bytes);
      if (!stream.ExpectMessage(PacketType)) return false;
      if (!stream.SshTryReadUint32Sync(out var id)) return false;
      if (!stream.SshTryReadStringSync(out var filename)) return false;
      if (!stream.SshTryReadUint32Sync(out var flagsRaw)) return false;
      if (!FileAttributes.TryParse(stream, out var attrs)) return false;
      value = new(id, filename, (OpenFlags)flagsRaw, attrs);
      return true;
   }
}
