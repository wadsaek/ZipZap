namespace ZipZap.FileService.Helpers;
using System;
using System.Threading.Tasks;

public abstract record Option<T>;
public sealed record None<T> : Option<T>;
public sealed record Some<T>(T data) : Option<T>;

public static class OptionExt {
    public static T UnwrapOr<T>(this Option<T> option, T fallback) => option switch {
        Some<T>(T data) => data,
        None<T> => fallback,
        _ => throw new InvalidEnumVariantException(nameof(Option<T>))

    };
    public static T UnwrapOrElse<T>(this Option<T> option, Func<T> otherwise) => option switch {
        Some<T>(T data) => data,
        None<T> => otherwise(),
        _ => throw new InvalidEnumVariantException(nameof(Option<T>))
    };

    public static Option<U> Select<T, U>(this Option<T> option, Func<T, U> selector) => option switch {
        Some<T>(T data) => new Some<U>(selector(data)),
        None<T> => new None<U>(),
        _ => throw new InvalidEnumVariantException(nameof(Option<T>))
    };
    public static Option<U> SelectMany<T, U>(this Option<T> option, Func<T, Option<U>> selector) => option switch {
        Some<T>(T data) => selector(data),
        None<T> => new None<U>(),
        _ => throw new InvalidEnumVariantException(nameof(Option<T>))
    };
    public static Option<T> Where<T>(this Option<T> opt, Func<T, bool> filter) =>
        opt.SelectMany<T, T>(data => filter(data) ? new Some<T>(data) : new None<T>());

    async public static Task<Option<T>> WhereAsync<T>(this Task<Option<T>> opt, Func<T,bool> filter)=>
        (await opt).Where(filter);
}
