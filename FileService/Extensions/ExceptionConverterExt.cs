using System;

using ZipZap.Classes.Helpers;
namespace ZipZap.FileService.Extensions;

public static class ExceptionConverterExt {
    extension<E>(ExceptionConverter<E> converter) {
        public ChainedExceptionConverter<E> After(Func<Exception, Option<E>> selector)
        => new(selector, converter);
    }
}
