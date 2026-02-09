using System.ComponentModel;

using ZipZap.Classes;
using ZipZap.LangExt.Helpers;

namespace ZipZap.Front.Services;

public abstract record FsoStatus {
    public sealed record Success(Fso Fso) : FsoStatus;
    public sealed record ParseError : FsoStatus;
    public sealed record StatusServiceError(ServiceError Error) : FsoStatus;

    public static FsoStatus FromServiceResult(Result<Fso, ServiceError> result)
        => result switch {
            Err<Fso, ServiceError>(var err) => new StatusServiceError(err),
            Ok<Fso, ServiceError>(var fso) => new Success(fso),
            _ => throw new InvalidEnumArgumentException()

        };
}

