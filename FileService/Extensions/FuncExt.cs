using System;

namespace ZipZap.FileService.Extensions;
public static class FuncExt {
    public static Func<T1, T3> Compose<T1, T2, T3>(this Func<T1, T2> fst, Func<T2, T3> snd)
        => a => snd(fst(a));
}
