namespace ZipZap.Classes.Helpers;

using System;
using System.Threading.Tasks;
using static OptionExt;

public abstract record Option<T> {
    public abstract bool IsSome();
    public static implicit operator Option<T>(T? t) =>
            t is null
            ? None<T>()
            : Some(t);
    public static implicit operator T?(Option<T> option) => option switch {
        Some<T>(T Data) => Data,
        None<T> => default,
        _ => throw new InvalidEnumVariantException(nameof(option))
    };
};
public sealed record None<T> : Option<T> {
    public override bool IsSome() => false;
}
public sealed record Some<T>(T Data) : Option<T> {
    public override bool IsSome() => true;
}

public static class OptionExt {
    extension<T>(Option<T> option) {
        public static Option<T> Some(T arg) {
            return new Some<T>(arg);
        }
        public static Option<T> None() {
            return new None<T>();
        }
        public T UnwrapOr(T fallback) => option switch {
            Some<T>(T data) => data,
            None<T> => fallback,
            _ => throw new InvalidEnumVariantException(nameof(Option<>))

        };
        public T UnwrapOrElse(Func<T> otherwise) => option switch {
            Some<T>(T data) => data,
            None<T> => otherwise(),
            _ => throw new InvalidEnumVariantException(nameof(Option<>))
        };

        public Option<U> Select<U>(Func<T, U> selector) => option switch {
            Some<T>(T data) => new Some<U>(selector(data)),
            None<T> => new None<U>(),
            _ => throw new InvalidEnumVariantException(nameof(Option<>))
        };
        public Option<U> SelectMany<U>(Func<T, Option<U>> selector) => option switch {
            Some<T>(T data) => selector(data),
            None<T> => new None<U>(),
            _ => throw new InvalidEnumVariantException(nameof(Option<>))
        };
        public Option<T> Where(Func<T, bool> filter) =>
        option.SelectMany<T, T>(data => filter(data) ? new Some<T>(data) : new None<T>());
    }

    extension<T>(Task<Option<T>> opt) {
        public async Task<Option<T>> WhereAsync(Func<T, bool> filter) =>
        (await opt).Where(filter);
    }

    extension<T>(T? t) where T : class {
        public Option<T> ToOption() =>
            t is null
            ? None<T>()
            : Some(t);
    }
    extension<T>(T? t) where T : struct {
        public Option<T> ToOption() =>
            t is null
            ? None<T>()
            : Some((T)t);
    }
}
