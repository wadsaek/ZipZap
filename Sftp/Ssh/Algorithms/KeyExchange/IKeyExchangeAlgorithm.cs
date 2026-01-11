using System;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ZipZap.Sftp.Ssh.Numbers;

namespace ZipZap.Sftp.Ssh.Algorithms;

public interface IKeyExchangeAlgorithm : INamed {
    Task<BigInteger?> ExchangeKeysAsync(SshState sshState, CancellationToken cancellationToken);
}

internal class DiffieHelmanGroup14Sha256 : IKeyExchangeAlgorithm {
    public static BigInteger Group { get; } = BigInteger.Parse("00FFFFFFFFFFFFFFFFC90FDAA22168C234C4C6628B80DC1CD129024E088A67CC74020BBEA63B139B22514A08798E3404DDEF9519B3CD3A431B302B0A6DF25F14374FE1356D6D51C245E485B576625E7EC6F44C42E9A637ED6B0BFF5CB6F406B7EDEE386BFB5A899FA5AE9F24117C4B1FE649286651ECE45B3DC2007CB8A163BF0598DA48361C55D39A69163FA8FD24CF5F83655D23DCA3AD961C62F356208552BB9ED529077096966D670C354E4ABC9804F1746C08CA18217C32905E462E36CE3BE39E772C180E86039B2783A2EC07A28FB5C55DF06F4C52C9DE2BCBF6955817183995497CEA956AE515D2261898FA051015728E5A8AACAA68FFFFFFFFFFFFFFFF", System.Globalization.NumberStyles.HexNumber);
    public static int Generator { get; } = 2;
    public NameList.Item Name => new NameList.GlobalName("diffie-hellman-group14-sha256");

    Task<BigInteger?> IKeyExchangeAlgorithm.ExchangeKeysAsync(SshState sshState, CancellationToken cancellationToken) {
        return new DiffieHelman(Group, Generator, SHA256.HashData).ExchangeKeysAsync(sshState, cancellationToken);
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

    public async Task<BigInteger?>
ExchangeKeysAsync(SshState state, CancellationToken cancellationToken) {
        var packetMaybeNull = await state.Stream.SshTryReadPacket(state.MacAlgorithm, cancellationToken);
        if (packetMaybeNull is not Packet clientInitPacket) return null;
        if (await KeyExchangeDiffieHelmanInit.TryFromPayload(clientInitPacket.Inner.Payload, cancellationToken) is not { E: var clientExponent }) return null;
        var order = Group.GetByteCount();
        var exponent = RandomNumberGenerator.GetInt32(1, order);
        var exponentiated = BigInteger.ModPow(Generator, exponent, Group);
        var secret = BigInteger.ModPow(clientExponent, exponent, Group);
        var signature = await GetSignature(state.IdenitificationStrings, state.ClientKexInit, state.ServerKexInit, state.HostKeyPair, clientExponent, exponentiated, secret, cancellationToken);
        var reply = new KeyExchangeDiffieHelmanReply(state.HostKeyPair.PublicKey, exponentiated, signature);
        var replyPacket = await reply.ToPacket(state.MacAlgorithm);
        await state.Stream.SshWritePacket(replyPacket, cancellationToken);
        return secret;
    }
    public async Task<byte[]> GetSignature(IdenitificationStrings idenitificationStrings, byte[] clientKexInit, byte[] serverKexInit, HostKeyPair hostKeyPair, BigInteger clientExponent, BigInteger exponentiated, BigInteger secret, CancellationToken cancellationToken) {
        var clientIdentityBytes = Encoding.UTF8.GetByteCount(idenitificationStrings.Client);
        var serverIdentityBytes = Encoding.UTF8.GetByteCount(idenitificationStrings.Server);
        var buffer = new byte[
            clientIdentityBytes
            + serverIdentityBytes
            + clientKexInit.Length
            + serverKexInit.Length
            + hostKeyPair.PublicKey.Length
            + clientExponent.GetByteCount() + 4
            + exponentiated.GetByteCount() + 4
            + secret.GetByteCount() + 4];
        await using var bufStream = new MemoryStream(buffer);
        await bufStream.SshWriteString(idenitificationStrings.Client, cancellationToken);
        await bufStream.SshWriteString(idenitificationStrings.Server, cancellationToken);
        await bufStream.SshWriteByteString(clientKexInit, cancellationToken);
        await bufStream.SshWriteByteString(serverKexInit, cancellationToken);
        await bufStream.SshWriteByteString(hostKeyPair.PublicKey, cancellationToken);
        await bufStream.SshWriteBigInt(clientExponent, cancellationToken);
        await bufStream.SshWriteBigInt(exponentiated, cancellationToken);
        await bufStream.SshWriteBigInt(secret, cancellationToken);

        var exchangeHash = Hash(buffer);
        var signed = hostKeyPair.Algorithm.Sign(exchangeHash, hostKeyPair.PrivateKey);

        return signed;
    }
}

public record HostKeyPair(byte[] PublicKey, byte[] PrivateKey, IPublicKeyAlgorithm Algorithm);

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

    public Task<byte[]> ToPayload(CancellationToken cancellationToken) {
        throw new NotImplementedException();
    }

    internal Task<Packet> ToPacket(IMacAlgorithm macAlgorithm) {
        throw new NotImplementedException();
    }
}
