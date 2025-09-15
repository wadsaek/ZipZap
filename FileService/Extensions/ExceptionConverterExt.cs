using System;
using ZipZap.FileService.Helpers;

namespace ZipZap.FileService.Extensions;

public static class ExceptionConverterExt {
    public static ChainedExceptionConverter<E> After<E>(this ExceptionConverter<E> converter, Func<Exception, Option<E>> selector)
        => new ChainedExceptionConverter<E>(selector, converter);
}
