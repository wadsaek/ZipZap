// ServiceRequest.cs - Part of the ZipZap project for storing files online
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

public record ServiceRequest(string ServiceName) : IClientPayload<ServiceRequest> {
    public static Message Message => Message.ServiceRequest;

    public static bool TryParse(byte[] payload, [NotNullWhen(true)] out ServiceRequest? value) {
        value = null;
        var stream = new MemoryStream(payload);
        if (!stream.SshTryReadByteSync(out var msg) || (Message)msg != Message) return false;
        if (!stream.SshTryReadStringSync(out var name)) return false;
        value = new(name);
        return true;
    }
}
