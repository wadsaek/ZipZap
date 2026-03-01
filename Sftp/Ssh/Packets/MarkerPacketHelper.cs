// MarkerPacketHelper.cs - Part of the ZipZap project for storing files online
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

namespace ZipZap.Sftp.Ssh;

internal class MarkerPacketHelper {

    public static bool TryParse<T>(byte[] payload, [NotNullWhen(true)] out T? value) where T : IClientPayload<T>, new() {
        value = default;
        var stream = new MemoryStream(payload);
        if (!stream.ExpectMessage(T.Message))
            return false;
        value = new();
        return true;
    }

    public static byte[] ToPayload<T>() where T : IServerPayload {
        return new SshMessageBuilder()
            .Write(T.Message)
            .Build();
    }
}
