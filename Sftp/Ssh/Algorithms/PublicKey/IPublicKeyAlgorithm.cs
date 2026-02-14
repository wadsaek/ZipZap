using System.Security.Cryptography;

namespace ZipZap.Sftp.Ssh.Algorithms;

public interface IPublicKeyAlgorithm : INamed {
    public bool Verify(byte[] unsigned, byte[] key);
}

public class RsaPublicKeyAlgorithm : IPublicKeyAlgorithm {
    public NameList.Item Name => new NameList.GlobalName("rsa-sha2-256");
    public HashAlgorithm HashAlgorithm => SHA256.Create();

    public bool Verify(byte[] unsigned, byte[] key) {
        throw new System.NotImplementedException();
    }
}
