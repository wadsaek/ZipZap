

using System.Collections.Generic;

namespace ZipZap.Classes.Extensions;

public static class StringExt {
    extension(string str) {
        public IEnumerable<string> SplitPath()
            => str
            .Split('/')
            .WhereNot(string.IsNullOrWhiteSpace);
        public string NormalizePath()
            => str.Trim('/');
    }
}
