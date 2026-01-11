using System.Collections.Generic;
using System.Linq;

namespace ZipZap.Sftp.Ssh;

public interface INamed {
    public NameList.Item Name { get; }
}

public static class INamedExt {
    extension(IEnumerable<INamed> named) {
        public NameList ToNameList() => new(named.Select(n => n.Name).ToArray());
    }
}
