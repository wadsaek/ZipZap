using System;

namespace ZipZap.FileService.Helpers;

public abstract class ExceptionConverter<Err> {
    public abstract Err Convert(Exception e);
}
sealed public class SimpleExceptionConverter<Err> : ExceptionConverter<Err> {
    private readonly Func<Exception, Err> _selector;
    public SimpleExceptionConverter(
        Func<Exception, Err> selector) {
        _selector = selector;
    }

    override public Err Convert(Exception e) => _selector(e);
}

sealed public class ChainedExceptionConverter<Err> : ExceptionConverter<Err> {
    private readonly Func<Exception, Option<Err>> _selector;
    private readonly ExceptionConverter<Err> _next;

    public ChainedExceptionConverter(
            Func<Exception, Option<Err>> selector,
            ExceptionConverter<Err> next) {
        _selector = selector;
        _next = next;
    }

    override public Err Convert(Exception e) => _selector(e)
        .UnwrapOrElse(() => _next.Convert(e));
}
