using System;

namespace ZipZap.Classes.Helpers;

public static class Assertions {
    public static void Assert(bool expression, string message = "Assserion failed") {
        if (!expression) throw new ArgumentException(message);
    }
    public static void AssertEq<T>(this T fst, T target, string? message = null)
        where T : IEquatable<T> {
        message ??= $"Assserion failed, fst={fst}, target={target}";
        Assert(fst.Equals(target), message);
    }
}
