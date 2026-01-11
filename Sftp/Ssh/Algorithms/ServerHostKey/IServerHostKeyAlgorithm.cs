namespace ZipZap.Sftp.Ssh.Algorithms;

public interface IServerHostKeyAlgorithm : IPublicKeyAlgorithm {
    public HostKeyPair GetHostKeyPair();
}

