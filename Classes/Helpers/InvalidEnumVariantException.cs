using System;

namespace ZipZap.Classes.Helpers;

public class InvalidEnumVariantException : Exception {
    public InvalidEnumVariantException() {
    }

    public InvalidEnumVariantException(string? message) : base(message) {
    }
}
