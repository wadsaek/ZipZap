// NewKeys.cs - Part of the ZipZap project for storing files online
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
