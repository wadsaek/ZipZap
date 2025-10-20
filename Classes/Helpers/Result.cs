namespace ZipZap.Classes.Helpers;

using System;
using System.ComponentModel;

public abstract record Result<T, E> {
    public static Result<T, E> Ok(T data) {
        return new Ok<T, E>(data);
    }
    public static Result<T, E> Err(E error) {
        return new Err<T, E>(error);
    }
};
public sealed record Ok<T, E>(T Data) : Result<T, E>;
public sealed record Err<T, E>(E Error) : Result<T, E>;

public static class ResultExt {
    public static Result<T, E> Try<T, E>(Func<T> function, ExceptionConverter<E> converter) {
        try {
            return Result<T, E>.Ok(function());
        } catch (Exception e) {
            return Result<T, E>.Err(converter.Convert(e));
        }

    }
    public static T UnwrapOr<T, E>(this Result<T, E> result, T fallback) => result switch {
        Ok<T, E>(T data) => data,
        Err<T, E> => fallback,
        _ => throw new InvalidEnumArgumentException(nameof(Result<,>))
    };

    public static T UnwrapOrElse<T, E>(this Result<T, E> result, Func<E, T> otherwise) => result switch {
        Ok<T, E>(T data) => data,
        Err<T, E>(E error) => otherwise(error),
        _ => throw new InvalidEnumArgumentException(nameof(Result<,>))
    };
    public static Result<U, E> Select<T, U, E>(this Result<T, E> result, Func<T, U> selector) => result switch {
        Ok<T, E>(T data) => new Ok<U, E>(selector(data)),
        Err<T, E>(E error) => new Err<U, E>(error),
        _ => throw new InvalidEnumArgumentException(nameof(Result<,>))

    };

    public static Result<U, E> SelectMany<T, U, E>(this Result<T, E> result, Func<T, Result<U, E>> selector) => result switch {
        Ok<T, E>(T data) => selector(data),
        Err<T, E>(E error) => new Err<U, E>(error),
        _ => throw new InvalidEnumArgumentException(nameof(Result<,>))
    };
}
