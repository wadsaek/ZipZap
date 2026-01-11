using System.Collections.Immutable;

namespace ZipZap.Sftp.Ssh.Algorithms;

public interface IProvider<T> where T : INamed {
    public IImmutableList<T> Items { get; }
}

