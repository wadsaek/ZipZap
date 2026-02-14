namespace ZipZap.LangExt.Helpers;

using System;
using System.ComponentModel;
using System.Threading.Tasks;

using static ResultConstructor;

public static class ResultConstructor {
    extension<T, E>(Result<T, E>) {
        public static Result<T, E> Ok(T arg) {
            return new Ok<T, E>(arg);
        }

        public static Result<T, E> Err(E err) {
            return new Err<T, E>(err);
        }
    }
}

public abstract record Result<T, E> {
    public static Result<T, E> Ok(T data) {
        return new Ok<T, E>(data);
    }
    public static Result<T, E> Err(E error) {
        return new Err<T, E>(error);
    }
}
public sealed record Ok<T, E>(T Data) : Result<T, E>;
public sealed record Err<T, E>(E Error) : Result<T, E>;

public static class ResultExt {
    extension<E>(E err) {
        public Result<T, E> AsErrorOf<T>() => Err<T, E>(err);
    }
    extension<T>(T data) {
        public Result<T, E> AsOkOf<E>() => Ok<T, E>(data);
    }

    extension<T, E>(Result<T, E> result) {
        public T UnwrapOr(T fallback) => result switch {
            Ok<T, E>(T data) => data,
            Err<T, E> => fallback,
            _ => throw new InvalidEnumArgumentException(nameof(Result<,>))
        };

        public T UnwrapOrElse(Func<E, T> otherwise) => result switch {
            Ok<T, E>(T data) => data,
            Err<T, E>(E error) => otherwise(error),
            _ => throw new InvalidEnumArgumentException(nameof(Result<,>))
        };
        public T? Unwrap() => result switch {
            Ok<T, E>(T data) => data,
            _ => default
        };


        public Result<U, E> Select<U>(Func<T, U> selector) => result switch {
            Ok<T, E>(T data) => new Ok<U, E>(selector(data)),
            Err<T, E>(E error) => new Err<U, E>(error),
            _ => throw new InvalidEnumArgumentException(nameof(Result<,>))

        };

        public Result<U, E> SelectMany<U>(Func<T, Result<U, E>> selector) => result switch {
            Ok<T, E>(T data) => selector(data),
            Err<T, E>(E error) => new Err<U, E>(error),
            _ => throw new InvalidEnumArgumentException(nameof(Result<,>))
        };
        public async Task<Result<U, E>> SelectAsync<U>(Func<T, Task<U>> selector) => result switch {
            Ok<T, E>(T data) => new Ok<U, E>(await selector(data)),
            Err<T, E>(E error) => new Err<U, E>(error),
            _ => throw new InvalidEnumArgumentException(nameof(Result<,>))
        };
        public async Task<Result<U, E>> SelectManyAsync<U>(Func<T, Task<Result<U, E>>> selector) => result switch {
            Ok<T, E>(T data) => await selector(data),
            Err<T, E>(E error) => new Err<U, E>(error),
            _ => throw new InvalidEnumArgumentException(nameof(Result<,>))
        };
    }
}
