using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace ZipZap.Sftp.Ssh.Algorithms;

public interface IMacAlgorithm : INamed {
    public int Length { get; }

    public bool EnsureCorrectMacFor(PacketWithoutMac packet, byte[] mac);

    public Task<byte[]> GenerateMacForAsync(uint sequencial, BigInteger secret, byte[] bytes, CancellationToken cancellationToken);
}
public class NoMacAlgorithm : IMacAlgorithm {
    public int Length { get; }

    public NameList.Item Name => new NameList.GlobalName("none");

    public bool EnsureCorrectMacFor(PacketWithoutMac packet, byte[] mac) => mac is [];

    public Task<byte[]> GenerateMacForAsync(uint _sshState, BigInteger _key, byte[] _bytes, CancellationToken _token) => Task.FromResult<byte[]>([]);
}

public class HMacSha2256EtmOpenSsh : IMacAlgorithm {
    public int Length => 256;

    public NameList.Item Name => new NameList.LocalName("hmac-sha2-256-etm", "openssh.com");

    // TODO: implement
    public bool EnsureCorrectMacFor(PacketWithoutMac packet, byte[] mac) {
        return true;
    }

    public async Task<byte[]> GenerateMacForAsync(uint sshState, BigInteger secret, byte[] bytes, CancellationToken cancellationToken) {
        var buffer = new byte[sizeof(uint) + bytes.Length];
        await using var stream = new MemoryStream(buffer);
        stream.SshWriteUint32Sync(sshState);
        stream.SshWriteArraySync(bytes);
        return await HMACSHA256.HashDataAsync(secret.ToByteArray(), stream, cancellationToken);
    }
}
