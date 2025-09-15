using System;

namespace ZipZap.FileService.Helpers;

public class InvalidEnumVariantException : Exception {
    public InvalidEnumVariantException() {
    }

    public InvalidEnumVariantException(string? message) : base(message) {
    }
}
