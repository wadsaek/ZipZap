using System.IO;
using System.Threading;
using System.Threading.Tasks;

using ZipZap.Sftp.Ssh.Numbers;

namespace ZipZap.Sftp.Ssh;

public record NewKeys : IServerPayload, IClientPayload<NewKeys> {
    public static Message Message => Message.Newkeys;

    public static async Task<NewKeys?> TryFromPayload(byte[] payload, CancellationToken cancellationToken) {
        var stream = new MemoryStream(payload);
        if (await stream.SshTryReadByte(cancellationToken) != (byte)Message) return null;
        return new();
    }

    public byte[] ToPayload() {
        return new SshMessageBuilder()
            .Write((byte)Message)
            .Build();
    }
}
