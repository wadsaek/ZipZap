using System.Collections.Generic;
using System.Linq;

namespace ZipZap.Classes.Extensions;

public static class IEnumerableExt {
    extension(IEnumerable<string> strings) {
        public string ConcatenateWith(string str) => strings.Aggregate((acc, next) => $"{acc}{str}{next}");
    }
}
