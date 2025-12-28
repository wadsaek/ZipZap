
using System;
using System.ComponentModel;
using System.Threading.Tasks;

using ZipZap.Classes;
using ZipZap.Classes.Extensions;
using ZipZap.Classes.Helpers;
using ZipZap.Front.Services;

namespace ZipZap.Front.Handlers.Files.View;

internal static class Shared {
    public static async Task<FsoStatus> GetFsoById(IBackend backend, string? path) {
        if (!Guid.TryParse(path, out var guid))
            return new FsoStatus.ParseError();
        return FsoStatus.FromServiceResult(await backend.GetFsoByIdAsync(guid.ToFsoId()));
    }
}

internal abstract record FsoStatus {
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
