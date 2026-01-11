using System.Threading;
using System.Threading.Tasks;

namespace ZipZap.Sftp.Ssh.Algorithms;

public interface IMacAlgorithm : INamed {
    public int Length { get; }

    public bool EnsureCorrectMacFor(PacketWithoutMac packet, byte[] mac);

    public Task<byte[]> GenerateMacFor(byte[] bytes, CancellationToken cancellationToken);
}
public class NoMacAlgorithm : IMacAlgorithm {
    public int Length { get; }

    public NameList.Item Name => new NameList.GlobalName("none");

    public bool EnsureCorrectMacFor(PacketWithoutMac packet, byte[] mac) => mac is [];

    public Task<byte[]> GenerateMacFor(byte[] _bytes, CancellationToken _token) => Task.FromResult<byte[]>([]);
}

public class HMacSha2256EtmOpenSsh : IMacAlgorithm {
    public int Length => 256;

    public NameList.Item Name => new NameList.LocalName("hmac-sha2-256-etm", "openssh.com");

    // TODO: implement
    public bool EnsureCorrectMacFor(PacketWithoutMac packet, byte[] mac) {
        return true;
    }

    public Task<byte[]> GenerateMacFor(byte[] bytes, CancellationToken cancellationToken) {
        throw new System.NotImplementedException();
    }
}
