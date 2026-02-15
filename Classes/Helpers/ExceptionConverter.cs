// ExceptionConverter.cs - Part of the ZipZap project for storing files online
//     Copyright (C) 2026  Barenboim Esther wadsaek@gmail.com
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY

using System;

using ZipZap.LangExt.Helpers;

namespace ZipZap.Classes.Helpers;

public abstract class ExceptionConverter<TErr> {
    public abstract TErr Convert(Exception e);
}
public sealed class SimpleExceptionConverter<TErr> : ExceptionConverter<TErr> {
    private readonly Func<Exception, TErr> _selector;
    public SimpleExceptionConverter(
        Func<Exception, TErr> selector) {
        _selector = selector;
    }

    public override TErr Convert(Exception e) => _selector(e);
}

public sealed class ChainedExceptionConverter<Err> : ExceptionConverter<Err> {
    private readonly Func<Exception, Err?> _selector;
    private readonly ExceptionConverter<Err> _next;

    public ChainedExceptionConverter(
            Func<Exception, Err?> selector,
            ExceptionConverter<Err> next) {
        _selector = selector;
        _next = next;
    }

    public override Err Convert(Exception e) => _selector(e) ?? _next.Convert(e);
}

public static class ExceptionConverterExt {
    extension<T, E>(Result<T, E>) {
        public static Result<T, E> Try(Func<T> function, ExceptionConverter<E> converter) {
            try {
                return Result<T, E>.Ok(function());
            } catch (Exception e) {
                return Result<T, E>.Err(converter.Convert(e));
            }
        }
    }
}
