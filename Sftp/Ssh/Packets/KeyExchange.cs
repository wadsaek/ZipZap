// KeyExchange.cs - Part of the ZipZap project for storing files online
//     Copyright (C) 2026  Barenboim Esther wadsaek@gmail.com
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY

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

    public byte[] ToPayload() {
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
        var buffer = new SshMessageBuilder()
            .Write((byte)Message)
            .WriteArray(Cookie)
            .WriteByteString(kexAlgorithms)
            .WriteByteString(serverHostKeyAlgorithmsAlgorithms)
            .WriteByteString(encryptionAlgorithmsClientToServer)
            .WriteByteString(encryptionAlgorithmsServerToClient)
            .WriteByteString(macAlgorithmsClientToServer)
            .WriteByteString(macAlgorithmsServerToClient)
            .WriteByteString(compressionAlgorithmsClientToServer)
            .WriteByteString(compressionAlgorithmsServerToClient)
            .WriteByteString(languagesClientToServer)
            .WriteByteString(languagesServerToClient)
            .Write(FirstKexPacketFollows)
            .Write(Value)
            .Build();
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
