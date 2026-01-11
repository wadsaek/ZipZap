namespace ZipZap.Sftp.Ssh.Algorithms;

public interface IPublicKeyAlgorithm : INamed {
    public byte[] Sign(byte[] unsigned, byte[] key);
}

public class RsaKeyAlgorithm : IPublicKeyAlgorithm {
    public NameList.Item Name => new NameList.GlobalName("rsa-sha2-512");

    public byte[] Sign(byte[] unsigned, byte[] key) {
        throw new System.NotImplementedException();
    }
}
