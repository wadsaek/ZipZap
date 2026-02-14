using System.Security.Cryptography;

namespace ZipZap.Sftp;

public interface ISftpConfiguration {
    int Port { get; }
    string ServerName { get; }
    string Version { get; }
    RSA? RsaKey { get { return null; } }
}

