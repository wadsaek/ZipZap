// DiffieHellmanPacket.cs - Part of the ZipZap project for storing files online
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
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

using ZipZap.Sftp.Ssh.Numbers;

namespace ZipZap.Sftp.Ssh.Algorithms;
internal record KeyExchangeDiffieHelmanInit(BigInteger E) : IPayload, IClientPayload<KeyExchangeDiffieHelmanInit> {
    public static Message Message => Message.KexDhInit;
    public static async Task<KeyExchangeDiffieHelmanInit?> TryFromPayload(byte[] payload, CancellationToken cancellationToken) {
        var stream = new MemoryStream(payload);
        if (await stream.SshTryReadByte(cancellationToken) != (byte)Message) return null;
        if (await stream.SshTryReadBigInt(cancellationToken) is not BigInteger e) return null;
        return new(e);
    }
    public static bool TryParse(byte[] payload, [NotNullWhen(true)] out KeyExchangeDiffieHelmanInit? packet) {
        packet = null;
        var stream = new MemoryStream(payload);
        if (!stream.SshTryReadByteSync(out var msg) || (Message)msg != Message) return false;
        if (!stream.SshTryReadBigIntSync(out var e)) return false;
        packet = new(e);
        return true;
    }
}
internal record KeyExchangeDiffieHelmanReply(byte[] PublicHostKey, BigInteger ServerExponent, byte[] HashSignature) : IServerPayload {
    public static Message Message => Message.KexDhReply;

    public byte[] ToPayload() {
        var buffer = new SshMessageBuilder()
            .Write((byte)Message)
            .WriteByteString(PublicHostKey)
            .Write(ServerExponent)
            .WriteByteString(HashSignature)
            .Build();

        return buffer;
    }
}
