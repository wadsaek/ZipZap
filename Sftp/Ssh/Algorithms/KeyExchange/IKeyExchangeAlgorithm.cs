using System;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using ZipZap.Sftp.Ext;
using ZipZap.Sftp.Ssh.Numbers;

namespace ZipZap.Sftp.Ssh.Algorithms;

public interface IKeyExchangeAlgorithm : INamed {
    Task<KeyExchangeResult?> ExchangeKeysAsync(SshState sshState, CancellationToken cancellationToken);
    Func<byte[], byte[]> Hash { get; }
}

public class DiffieHelmanGroup14Sha256 : IKeyExchangeAlgorithm {
    public static BigInteger Group { get; } = BigInteger.Parse("00FFFFFFFFFFFFFFFFC90FDAA22168C234C4C6628B80DC1CD129024E088A67CC74020BBEA63B139B22514A08798E3404DDEF9519B3CD3A431B302B0A6DF25F14374FE1356D6D51C245E485B576625E7EC6F44C42E9A637ED6B0BFF5CB6F406B7EDEE386BFB5A899FA5AE9F24117C4B1FE649286651ECE45B3DC2007CB8A163BF0598DA48361C55D39A69163FA8FD24CF5F83655D23DCA3AD961C62F356208552BB9ED529077096966D670C354E4ABC9804F1746C08CA18217C32905E462E36CE3BE39E772C180E86039B2783A2EC07A28FB5C55DF06F4C52C9DE2BCBF6955817183995497CEA956AE515D2261898FA051015728E5A8AACAA68FFFFFFFFFFFFFFFF", System.Globalization.NumberStyles.HexNumber);
    public static int Generator { get; } = 2;
    public NameList.Item Name => new NameList.GlobalName("diffie-hellman-group14-sha256");

    public Func<byte[], byte[]> Hash => SHA256.HashData;

    Task<KeyExchangeResult?> IKeyExchangeAlgorithm.ExchangeKeysAsync(SshState sshState, CancellationToken cancellationToken) {
        return new DiffieHelman(Group, Generator, Hash).ExchangeKeysAsync(sshState, cancellationToken);
    }
}

internal class DiffieHelman {
    public BigInteger Group { get; }
    public int Generator { get; }
    public Func<byte[], byte[]> Hash { get; }

    public DiffieHelman(BigInteger group, int generator, Func<byte[], byte[]> hash) {
        Hash = hash;
        Group = group;
        Generator = generator;
    }

    public async Task<KeyExchangeResult?> ExchangeKeysAsync(SshState state, CancellationToken cancellationToken) {
        var packetMaybeNull = await state.Stream.SshTryReadPacket(state.MacData.MacAlgorithm, cancellationToken);
        if (packetMaybeNull is not Packet clientInitPacket) return null;
        if (await KeyExchangeDiffieHelmanInit.TryFromPayload(clientInitPacket.Inner.Payload, cancellationToken) is not { E: var clientExponent }) return null;
        var order = (Group - 1) / 2;
        var exponent = BigInteger.Random(1, order);
        var exponentiated = BigInteger.ModPow(Generator, exponent, Group);
        var secret = BigInteger.ModPow(clientExponent, exponent, Group);
        var exchangeHash = await GetExchangeHash(state.IdenitificationStrings, state.ClientKexInit, state.ServerKexInit, state.HostKeyPair, clientExponent, exponentiated, secret, cancellationToken);
        var signature = state.HostKeyPair.Sign(exchangeHash);
        var publicKey = await state.HostKeyPair.GetPublicKeyBytes(cancellationToken);
        var reply = new KeyExchangeDiffieHelmanReply(publicKey, exponentiated, signature);
        var replyPacket = await reply.ToPacket(state, cancellationToken);
        await state.Stream.SshWritePacket(replyPacket, cancellationToken);
        return new(secret, exchangeHash);
    }

    public async Task<byte[]> GetExchangeHash(IdenitificationStrings idenitificationStrings, byte[] clientKexInit, byte[] serverKexInit, IHostKeyPair hostKeyPair, BigInteger clientExponent, BigInteger exponentiated, BigInteger secret, CancellationToken cancellationToken) {
        var publicKey = await hostKeyPair.GetPublicKeyBytes(cancellationToken);
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

public record KeyExchangeResult(BigInteger Secret, byte[] ExchangeHash);

public interface IHostKeyPair {
    Task<byte[]> GetPublicKeyBytes(CancellationToken cancellationToken);
    byte[] Sign(byte[] unsigned);
}

internal record KeyExchangeDiffieHelmanInit(BigInteger E) : IPayload, IClientPayload<KeyExchangeDiffieHelmanInit> {
    public static Message Message => Message.KexDhInit;
    public static async Task<KeyExchangeDiffieHelmanInit?> TryFromPayload(byte[] payload, CancellationToken cancellationToken) {
        var stream = new MemoryStream(payload);
        if (await stream.SshTryReadByte(cancellationToken) != (byte)Message) return null;
        if (await stream.SshTryReadBigInt(cancellationToken) is not BigInteger e) return null;
        return new(e);
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
