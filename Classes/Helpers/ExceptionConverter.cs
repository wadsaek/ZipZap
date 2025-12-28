using System;

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
