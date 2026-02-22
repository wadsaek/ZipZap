// KeyExchange.cs - Part of the ZipZap project for storing files online
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
using System.Threading;
using System.Threading.Tasks;

using ZipZap.Sftp.Ssh.Numbers;

namespace ZipZap.Sftp.Ssh;

public record KeyExchange(
    byte[] Cookie,
    NameList KexAlgorithms,
    NameList ServerHostKeyAlgorithms,
    NameList EncryptionAlgorithmsCtS,
    NameList EncryptionAlgorithmsStC,
    NameList MacAlgorithmsCtS,
    NameList MacAlgorithmsStC,
    NameList CompressionAlgorithmsCtS,
    NameList CompressionAlgorithmsStC,
    NameList LanguagesCtS,
    NameList LanguagesStC,
    bool FirstKexPacketFollows,
    uint Value
) : IServerPayload, IClientPayload<KeyExchange> {
    public static Message Message => Message.Kexinit;
    public static int CookieSize => 16;

    public byte[] ToPayload() {
        var buffer = new SshMessageBuilder()
            .Write((byte)Message)
            .WriteArray(Cookie)
            .WriteByteString(KexAlgorithms)
            .WriteByteString(ServerHostKeyAlgorithms)
            .WriteByteString(EncryptionAlgorithmsCtS)
            .WriteByteString(EncryptionAlgorithmsStC)
            .WriteByteString(MacAlgorithmsCtS)
            .WriteByteString(MacAlgorithmsStC)
            .WriteByteString(CompressionAlgorithmsCtS)
            .WriteByteString(CompressionAlgorithmsStC)
            .WriteByteString(LanguagesCtS)
            .WriteByteString(LanguagesStC)
            .Write(FirstKexPacketFollows)
            .Write(Value)
            .Build();
        return buffer;
    }

    public static bool TryParse(byte[] payload, [NotNullWhen(true)] out KeyExchange? value) {
        value = null;
        using var stream = new MemoryStream(payload);
        if (!stream.SshTryReadByteSync(out var msg)) return false;
        if ((Message)msg != Message.Kexinit) return false;
        var cookie = new byte[CookieSize];
        if (!stream.SshTryReadArraySync(cookie)) return false;
        if (!stream.SshTryReadNameListSync(out var kexAlgorithms)) return false;
        if (!stream.SshTryReadNameListSync(out var serverHostKeyAlgorithms)) return false;
        if (!stream.SshTryReadNameListSync(out var encryptionAlgorithmsClientToServer)) return false;
        if (!stream.SshTryReadNameListSync(out var encryptionAlgorithmsServerToClient)) return false;
        if (!stream.SshTryReadNameListSync(out var macAlgorithmsClientToServer)) return false;
        if (!stream.SshTryReadNameListSync(out var macAlgorithmsServerToClient)) return false;
        if (!stream.SshTryReadNameListSync(out var compressionAlgorithmsClientToServer)) return false;
        if (!stream.SshTryReadNameListSync(out var compressionAlgorithmsServerToClient)) return false;
        if (!stream.SshTryReadNameListSync(out var languagesClientToServer)) return false;
        if (!stream.SshTryReadNameListSync(out var languagesServerToClient)) return false;
        if (!stream.SshTryReadBoolSync(out var firstKexPacketFollows)) return false;
        if (!stream.SshTryReadUint32Sync(out var val)) return false;
        value = new(
            cookie,
            kexAlgorithms,
            serverHostKeyAlgorithms,
            encryptionAlgorithmsClientToServer,
            encryptionAlgorithmsServerToClient,
            macAlgorithmsClientToServer,
            macAlgorithmsServerToClient,
            compressionAlgorithmsClientToServer,
            compressionAlgorithmsServerToClient,
            languagesClientToServer,
            languagesServerToClient,
            firstKexPacketFollows,
            val
        );
        return true;
    }
}
