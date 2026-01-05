using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ZipZap.Sftp.Ssh;

public readonly struct Packet {
    public uint Length => sizeof(byte) + (uint)Payload.Length + (uint)Padding.Length;
    public byte PaddingLength => (byte)Padding.Length;
    public byte[] Payload { get; init; }
    public byte[] Padding { get; init; }
    public byte[] MAC { get; init; }
}

public static class PacketExt {
    extension(Stream stream) {
        public async Task SshWritePacket(Packet packet, CancellationToken cancellationToken) {
            await stream.SshWriteUint32(packet.Length, cancellationToken);
            await stream.SshWriteByte(packet.PaddingLength, cancellationToken);
            await stream.SshWriteArray(packet.Payload, cancellationToken);
            await stream.SshWriteArray(packet.Padding, cancellationToken);
            await stream.SshWriteArray(packet.Padding, cancellationToken);
            await stream.SshWriteArray(packet.MAC, cancellationToken);
        }
        public async Task<Packet?> SshTryReadPacket(int macLength, CancellationToken cancellationToken) {
            if (await stream.SshTryReadUint32(cancellationToken) is not uint length) return null;
            if (await stream.SshTryReadByte(cancellationToken) is not byte padding_length) return null;

            var payload = new byte[(int)length - padding_length - 1];
            await stream.SshTryReadArray(payload, cancellationToken);

            var random_padding = new byte[padding_length];
            await stream.SshTryReadArray(random_padding, cancellationToken);

            var mac = new byte[macLength];
            await stream.SshTryReadArray(mac, cancellationToken);
            return new Packet {
                Payload = payload,
                Padding = random_padding,
                MAC = mac
            };
        }
    }
}
