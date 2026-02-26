// Generic.cs - Part of the ZipZap project for storing files online
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

using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

using ZipZap.Sftp.Ext;

namespace ZipZap.Sftp.Ssh.Algorithms;

internal class DiffieHelman {
    public BigInteger Group { get; }
    public int Generator { get; }
    public Func<byte[], byte[]> Hash { get; }

    public DiffieHelman(BigInteger group, int generator, Func<byte[], byte[]> hash) {
        Hash = hash;
        Group = group;
        Generator = generator;
    }

    public async Task<KeyExchangeResult?> ExchangeKeysAsync(IHostKeyPair hostKeyPair, KexInitPair kexInitPair, KeyExchangeInput input, CancellationToken cancellationToken) {
        if (await input.Reader.ReadUntilPacket<KeyExchangeDiffieHelmanInit>(cancellationToken)
            is not { Item2.E: var clientExponent })
            return null;

        var order = (Group - 1) / 2;
        var exponent = BigInteger.Random(1, order);
        var exponentiated = BigInteger.ModPow(Generator, exponent, Group);
        var secret = BigInteger.ModPow(clientExponent, exponent, Group);
        var exchangeHash = GetExchangeHash(
            input.IdenitificationStrings,
            kexInitPair.ClientKexInit, kexInitPair.ServerKexInit,
            hostKeyPair,
            clientExponent, exponentiated,
            secret
        );
        var signature = hostKeyPair.Sign(exchangeHash);
        var publicKey = hostKeyPair.GetPublicKey().ToByteString();
        var reply = new KeyExchangeDiffieHelmanReply(publicKey, exponentiated, signature);
        await input.Reader.SendPacket(reply, cancellationToken);
        return new(secret, exchangeHash);
    }

    public byte[] GetExchangeHash(IdenitificationStrings idenitificationStrings, byte[] clientKexInit, byte[] serverKexInit, IHostKeyPair hostKeyPair, BigInteger clientExponent, BigInteger exponentiated, BigInteger secret) {
        var publicKey = hostKeyPair.GetPublicKey().ToByteString();
        var buffer = new SshMessageBuilder()
            .Write(idenitificationStrings.Client)
            .Write(idenitificationStrings.Server)
            .WriteByteString(clientKexInit)
            .WriteByteString(serverKexInit)
            .WriteByteString(publicKey)
            .Write(clientExponent)
            .Write(exponentiated)
            .Write(secret)
            .Build();

        var exchangeHash = Hash(buffer);

        return exchangeHash;
    }
}

public record KexInitPair(byte[] ClientKexInit, byte[] ServerKexInit);
