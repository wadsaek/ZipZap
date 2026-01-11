using System.IO;
using System.Threading;
using System.Threading.Tasks;

using ZipZap.Sftp.Ssh.Numbers;

namespace ZipZap.Sftp.Ssh;

public record KeyExchange(
    byte[] Cookie,
    NameList KexAlgorithms,
    NameList ServerHostKeyAlgorithms,
    NameList EncryptionAlgorithmsClientToServer,
    NameList EncryptionAlgorithmsServerToClient,
    NameList MacAlgorithmsClientToServer,
    NameList MacAlgorithmsServerToClient,
    NameList CompressionAlgorithmsClientToServer,
    NameList CompressionAlgorithmsServerToClient,
    NameList LanguagesClientToServer,
    NameList LanguagesServerToClient,
    bool FirstKexPacketFollows,
    uint Value
) : IServerPayload, IClientPayload<KeyExchange> {
    public static Message Message => Message.Kexinit;
    public static int CookieSize => 16;

    public async Task<byte[]> ToPayload(CancellationToken cancellationToken) {
        var kexAlgorithms = KexAlgorithms.ToByteString();
        var serverHostKeyAlgorithmsAlgorithms = ServerHostKeyAlgorithms.ToByteString();
        var encryptionAlgorithmsClientToServer = EncryptionAlgorithmsClientToServer.ToByteString();
        var encryptionAlgorithmsServerToClient = EncryptionAlgorithmsServerToClient.ToByteString();
        var macAlgorithmsClientToServer = MacAlgorithmsClientToServer.ToByteString();
        var macAlgorithmsServerToClient = MacAlgorithmsServerToClient.ToByteString();
        var compressionAlgorithmsClientToServer = CompressionAlgorithmsClientToServer.ToByteString();
        var compressionAlgorithmsServerToClient = CompressionAlgorithmsServerToClient.ToByteString();
        var languagesClientToServer = LanguagesClientToServer.ToByteString();
        var languagesServerToClient = LanguagesServerToClient.ToByteString();
        var length = 1 + 16 +
                4 + kexAlgorithms.Length +
                4 + serverHostKeyAlgorithmsAlgorithms.Length +
                4 + encryptionAlgorithmsClientToServer.Length +
                4 + encryptionAlgorithmsServerToClient.Length +
                4 + macAlgorithmsClientToServer.Length +
                4 + macAlgorithmsServerToClient.Length +
                4 + compressionAlgorithmsClientToServer.Length +
                4 + compressionAlgorithmsServerToClient.Length +
                4 + languagesClientToServer.Length +
                4 + languagesServerToClient.Length +
                1 + 4;
        var buffer = new byte[length];
        using var stream = new MemoryStream(buffer);
        await stream.SshWriteByte((byte)Message, cancellationToken);
        await stream.SshWriteArray(Cookie, cancellationToken);

        await stream.SshWriteByteString(kexAlgorithms, cancellationToken);
        await stream.SshWriteByteString(serverHostKeyAlgorithmsAlgorithms, cancellationToken);
        await stream.SshWriteByteString(encryptionAlgorithmsClientToServer, cancellationToken);
        await stream.SshWriteByteString(encryptionAlgorithmsServerToClient, cancellationToken);
        await stream.SshWriteByteString(macAlgorithmsClientToServer, cancellationToken);
        await stream.SshWriteByteString(macAlgorithmsServerToClient, cancellationToken);
        await stream.SshWriteByteString(compressionAlgorithmsClientToServer, cancellationToken);
        await stream.SshWriteByteString(compressionAlgorithmsServerToClient, cancellationToken);
        await stream.SshWriteByteString(languagesClientToServer, cancellationToken);
        await stream.SshWriteByteString(languagesServerToClient, cancellationToken);
        await stream.SshWriteBool(FirstKexPacketFollows, cancellationToken);
        await stream.SshWriteUint32(Value, cancellationToken);
        return buffer;
    }
    public static async Task<KeyExchange?> TryFromPayload(byte[] payload, CancellationToken cancellationToken) {
        await using var stream = new MemoryStream(payload);
        var msg = (Message?)await stream.SshTryReadByte(cancellationToken);
        if (msg is not Message.Kexinit) return null;
        var cookie = new byte[CookieSize];
        if (!await stream.SshTryReadArray(cookie, cancellationToken)) return null;
        if (await stream.SshTryReadNameList(cancellationToken) is not NameList kexAlgorithms) return null;
        if (await stream.SshTryReadNameList(cancellationToken) is not NameList serverHostKeyAlgorithms) return null;
        if (await stream.SshTryReadNameList(cancellationToken) is not NameList encryptionAlgorithmsClientToServer) return null;
        if (await stream.SshTryReadNameList(cancellationToken) is not NameList encryptionAlgorithmsServerToClient) return null;
        if (await stream.SshTryReadNameList(cancellationToken) is not NameList macAlgorithmsClientToServer) return null;
        if (await stream.SshTryReadNameList(cancellationToken) is not NameList macAlgorithmsServerToClient) return null;
        if (await stream.SshTryReadNameList(cancellationToken) is not NameList compressionAlgorithmsClientToServer) return null;
        if (await stream.SshTryReadNameList(cancellationToken) is not NameList compressionAlgorithmsServerToClient) return null;
        if (await stream.SshTryReadNameList(cancellationToken) is not NameList languagesClientToServer) return null;
        if (await stream.SshTryReadNameList(cancellationToken) is not NameList languagesServerToClient) return null;
        if (await stream.SshTryReadBool(cancellationToken) is not bool firstKexPacketFollows) return null;
        if (await stream.SshTryReadUint32(cancellationToken) is not uint val) return null;
        return new(
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
    }
}
