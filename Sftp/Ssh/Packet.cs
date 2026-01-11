using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using ZipZap.Sftp.Ssh.Algorithms;

namespace ZipZap.Sftp.Ssh;

public record PacketWithoutMac(byte[] Payload, byte[] Padding) {

    public uint Length => sizeof(byte) + (uint)Payload.Length + (uint)Padding.Length;
    public byte PaddingLength => (byte)Padding.Length;
    public PacketWithoutMac(byte[] Payload) : this(Payload, []) {
        var paddingLength = 8 - ((Length + 4) % 8) + 8;
        Padding = RandomNumberGenerator.GetBytes((int)paddingLength);
    }
    public async Task<byte[]> ToByteString(CancellationToken cancellationToken) {
        byte[] buffer = new byte[Length + 4];
        using var stream = new MemoryStream(buffer);
        await stream.SshWriteUint32(Length, cancellationToken);
        await stream.SshWriteByte(PaddingLength, cancellationToken);
        await stream.SshWriteArray(Payload, cancellationToken);
        await stream.SshWriteArray(Padding, cancellationToken);
        return buffer;
    }
}
public record Packet(PacketWithoutMac Inner, byte[] Mac) {
    public uint Length => Inner.Length;
    public byte PaddingLength => Inner.PaddingLength;
    public Packet(byte[] Payload, byte[] Mac) : this(new PacketWithoutMac(Payload), Mac) { }
    public Packet(byte[] Payload, byte[] Padding, byte[] Mac) : this(new PacketWithoutMac(Payload, Padding), Mac) { }
}

public static class PacketExt {
    extension(Stream stream) {
        public async Task SshWritePacket(Packet packet, CancellationToken cancellationToken) {
            var rawPacket = await packet.Inner.ToByteString(cancellationToken);
            await stream.SshWriteArray(rawPacket, cancellationToken);
            await stream.SshWriteArray(packet.Mac, cancellationToken);
        }
        public async Task<Packet?> SshTryReadPacket(IMacAlgorithm macAlgorithm, CancellationToken cancellationToken) {
            if (await stream.SshTryReadUint32(cancellationToken) is not uint length) return null;
            if (await stream.SshTryReadByte(cancellationToken) is not byte paddingLength) return null;

            var payload = new byte[(int)length - paddingLength - 1];
            await stream.SshTryReadArray(payload, cancellationToken);

            var randomPadding = new byte[paddingLength];
            await stream.SshTryReadArray(randomPadding, cancellationToken);

            var mac = new byte[macAlgorithm.Length];
            await stream.SshTryReadArray(mac, cancellationToken);
            var rawPacket = new PacketWithoutMac(payload,randomPadding);
            if (!macAlgorithm.EnsureCorrectMacFor(rawPacket, mac)) return null;
            return new Packet(payload, randomPadding, mac);
        }
    }
}
