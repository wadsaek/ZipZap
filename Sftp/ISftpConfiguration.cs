namespace ZipZap.Sftp;

public partial class SftpService {
    public interface ISftpConfiguration {
        int Port { get; }
        string ServerName { get; }
    }
}

