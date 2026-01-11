using System.Security.Cryptography;

namespace ZipZap.Sftp.Ssh.Algorithms;

public interface IPublicKeyAlgorithm : INamed {
    public byte[] Sign(byte[] unsigned, byte[] key);
}

public class RsaKeyAlgorithm : IServerHostKeyAlgorithm {
    public NameList.Item Name => new NameList.GlobalName("rsa-sha2-512");

    public HostKeyPair GetHostKeyPair() {
        throw new System.NotImplementedException();
    }

    public byte[] Sign(byte[] unsigned, byte[] key) {
        var rsa = RSA.Create(new RSAParameters{});
    }
}
