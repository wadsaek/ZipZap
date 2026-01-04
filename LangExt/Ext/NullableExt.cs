using System;
using System.Threading.Tasks;

namespace ZipZap.LangExt.Extensions;

public static class NullableExt {
    extension<T>(T? t) where T : class {
        public T? Where(Func<T, bool> func) => t switch {
            null => null,
            var t1 => func(t1) ? t1 : null
        };
        public async Task<T?> WhereAsync(Func<T, Task<bool>> func) => t switch {
            null => null,
            var t1 => await func(t1) ? t1 : null
        };
    }
}
