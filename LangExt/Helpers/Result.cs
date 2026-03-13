// Result.cs - Part of the ZipZap project for storing files online
//     Copyright (C) 2026  Barenboim Esther wadsaek@gmail.com
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY, without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
//
//     You should have received a copy of the GNU General Public License
//     along with this program.  If not, see <https://www.gnu.org/licenses/>.

namespace ZipZap.LangExt.Helpers;

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
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
        public async Task<T> UnwrapOrElseAsync(Func<E, Task<T>> otherwise) => result switch {
            Ok<T, E>(T data) => data,
            Err<T, E>(E error) => await otherwise(error),
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

        public Result<T, U> SelectErr<U>(Func<E, U> selector) => result switch {
            Ok<T, E>(T data) => new Ok<T, U>(data),
            Err<T, E>(E error) => new Err<T, U>(selector(error)),
            _ => throw new InvalidEnumArgumentException(nameof(Result<,>))
        };
        public async Task<Result<T, U>> SelectErrAsync<U>(Func<E, Task<U>> selector) => result switch {
            Ok<T, E>(T data) => new Ok<T, U>(data),
            Err<T, E>(E error) => new Err<T, U>(await selector(error)),
            _ => throw new InvalidEnumArgumentException(nameof(Result<,>))
        };
        public Result<T, U> ErrSelectMany<U>(Func<E, Result<T, U>> selector) => result switch {
            Ok<T, E>(T data) => new Ok<T, U>(data),
            Err<T, E>(E error) => selector(error),
            _ => throw new InvalidEnumArgumentException(nameof(Result<,>))
        };
        public async Task<Result<T, U>> ErrSelectManyAsync<U>(Func<E, Task<Result<T, U>>> selector) => result switch {
            Ok<T, E>(T data) => new Ok<T, U>(data),
            Err<T, E>(E error) => await selector(error),
            _ => throw new InvalidEnumArgumentException(nameof(Result<,>))
        };
    }
    extension<T, E>(Task<Result<T, E>> result) {
        public async Task<Result<U, E>> SelectAsync<U>(Func<T, U> selector)
            => (await result).Select(selector);

        public async Task<Result<U, E>> SelectAsync<U>(Func<T, Task<U>> selector)
            => await (await result).SelectAsync(selector);

        public async Task<Result<U, E>> SelectManyAsync<U>(Func<T, Task<Result<U, E>>> selector)
            => await (await result).SelectManyAsync(selector);
        public async Task<Result<U, E>> SelectManyAsync<U>(Func<T, Result<U, E>> selector)
            => await result.SelectManyAsync(selector);

        public async Task<Result<T, U>> SelectErrAsync<U>(Func<E, U> selector)
            => (await result).SelectErr(selector);

        public async Task<Result<T, U>> SelectErrAsync<U>(Func<E, Task<U>> selector)
            => await (await result).SelectErrAsync(selector);

        public async Task<Result<T, U>> ErrSelectManyAsync<U>(Func<E, Task<Result<T, U>>> selector) => await result switch {
            Ok<T, E>(T data) => new Ok<T, U>(data),
            Err<T, E>(E error) => await selector(error),
            _ => throw new InvalidEnumArgumentException(nameof(Result<,>))
        };

        // sometimes you want to just throw exceptions,
        // this requires one less await to do so,
        // but both methods' signatures fit the goal
        [OverloadResolutionPriority(1)]
        public async Task<T> UnwrapOrElseAsync(Func<E, T> selector)
            => (await result).UnwrapOrElse(selector);

        public async Task<T> UnwrapOrElseAsync(Func<E, Task<T>> selector)
            => await (await result).UnwrapOrElseAsync(selector);


        public async Task<T?> UnwrapAsync()
            => (await result).Unwrap();
    };
}
