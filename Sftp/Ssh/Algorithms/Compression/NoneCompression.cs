using System.Threading;

namespace ZipZap.Sftp.Ssh.Algorithms;

public class NoneCompression : ICompressionAlgorithm {
    public NameList.Item Name => new NameList.GlobalName("none");

    public byte[] CompressAsync(byte[] bytes, CancellationToken cancellationToken) {
        return bytes;
    }

    public byte[] DecompressAsync(byte[] bytes, CancellationToken cancellationToken) {
        return bytes;
    }
}
