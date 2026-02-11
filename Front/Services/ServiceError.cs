using System;

namespace ZipZap.Front.Services;

public abstract record ServiceError {
    public sealed record FailedPrecondition(string Detail) : ServiceError;
    public sealed record Unauthorized : ServiceError;
    public sealed record NotFound : ServiceError;
    public sealed record BadResult : ServiceError;
    public sealed record BadRequest : ServiceError;
    public sealed record AlreadyExists : ServiceError;
    public sealed record Unknown(Exception Exception) : ServiceError;
}
