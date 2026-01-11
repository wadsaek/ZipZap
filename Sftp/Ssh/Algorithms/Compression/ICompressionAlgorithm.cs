using System.Threading;

namespace ZipZap.Sftp.Ssh.Algorithms;

public interface ICompressionAlgorithm : INamed {
    public byte[] CompressAsync(byte[] bytes, CancellationToken cancellationToken);
    public byte[] DecompressAsync(byte[] bytes, CancellationToken cancellationToken);
}

