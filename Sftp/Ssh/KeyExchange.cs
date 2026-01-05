using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using ZipZap.Sftp.Ssh.Numbers;

namespace ZipZap.Sftp.Ssh;

public record KeyExchange(
    byte[] Cookie,
    NameList KEXAlgorithms,
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
) : IPayload {
    public static Message Message => Message.Kexinit;
    public static int CookieSize => 16;
}

public static class KeyExchangeExt {
    extension(KeyExchange exchange) {
        public static async Task<KeyExchange?> TryFromPayload(byte[] payload, CancellationToken cancellationToken) {
            await using var stream = new MemoryStream(payload);
            var msg = (Message?)await stream.SshTryReadByte(cancellationToken);
            if (msg is not Message.Kexinit) return null;
            var cookie = new byte[KeyExchange.CookieSize];
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
        public async Task<byte[]> ToPayload(CancellationToken cancellationToken) {
            byte[] cookie = RandomNumberGenerator.GetBytes(16);
            byte[] kexAlgorithms = exchange.KEXAlgorithms.ToByteString();
            byte[] serverHostKeyAlgorithmsAlgorithms = exchange.ServerHostKeyAlgorithms.ToByteString();
            byte[] encryptionAlgorithmsClientToServer = exchange.EncryptionAlgorithmsClientToServer.ToByteString();
            byte[] encryptionAlgorithmsServerToClient = exchange.EncryptionAlgorithmsServerToClient.ToByteString();
            byte[] macAlgorithmsClientToServer = exchange.MacAlgorithmsClientToServer.ToByteString();
            byte[] macAlgorithmsServerToClient = exchange.MacAlgorithmsServerToClient.ToByteString();
            byte[] compressionAlgorithmsClientToServer = exchange.CompressionAlgorithmsClientToServer.ToByteString();
            byte[] compressionAlgorithmsServerToClient = exchange.CompressionAlgorithmsServerToClient.ToByteString();
            byte[] languagesClientToServer = exchange.LanguagesClientToServer.ToByteString();
            byte[] languagesServerToClient = exchange.LanguagesServerToClient.ToByteString();
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
            byte[] buffer = new byte[length];
            using var stream = new MemoryStream(buffer);
            await stream.SshWriteByte((byte)KeyExchange.Message, cancellationToken);
            await stream.SshWriteArray(cookie, cancellationToken);

            await stream.SshWriteByteString(serverHostKeyAlgorithmsAlgorithms, cancellationToken);
            await stream.SshWriteByteString(encryptionAlgorithmsClientToServer, cancellationToken);
            await stream.SshWriteByteString(encryptionAlgorithmsServerToClient, cancellationToken);
            await stream.SshWriteByteString(macAlgorithmsClientToServer, cancellationToken);
            await stream.SshWriteByteString(macAlgorithmsServerToClient, cancellationToken);
            await stream.SshWriteByteString(compressionAlgorithmsClientToServer, cancellationToken);
            await stream.SshWriteByteString(compressionAlgorithmsServerToClient, cancellationToken);
            await stream.SshWriteByteString(languagesClientToServer, cancellationToken);
            await stream.SshWriteByteString(languagesServerToClient, cancellationToken);
            await stream.SshWriteBool(exchange.FirstKexPacketFollows, cancellationToken);
            await stream.SshWriteUint32(exchange.Value, cancellationToken);
            return buffer;
        }

    }
}
