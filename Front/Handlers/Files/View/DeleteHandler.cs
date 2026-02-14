using System.ComponentModel;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

using ZipZap.Classes;
using ZipZap.Classes.Helpers;
using ZipZap.Front.Factories;
using ZipZap.Front.Services;
using ZipZap.LangExt.Helpers;

using static ZipZap.LangExt.Helpers.ResultConstructor;

namespace ZipZap.Front.Handlers.Files.View;

public static class DeleteHandler {

    public abstract record DeleteHandlerError {
        public sealed record NotFound : DeleteHandlerError;
        public sealed record BadRequest : DeleteHandlerError;
        public sealed record Internal : DeleteHandlerError;

    }

    public static async Task<Result<Unit, DeleteHandlerError>> OnDeleteAsync(FsoId id, HttpRequest request, IFactory<IBackend, BackendConfiguration> backendFactory) {
        var token = request.Cookies[Constants.AUTHORIZATION];
        if (token is null)
            return Err<Unit, DeleteHandlerError>(new DeleteHandlerError.NotFound());

        var backend = backendFactory.Create(new(token));
        var status = FsoStatus.FromServiceResult(await backend.DeleteFso(id, DeleteFlags.Empty));

        return status switch {
            FsoStatus.ParseError => Err<Unit, DeleteHandlerError>(new DeleteHandlerError.BadRequest()),
            FsoStatus.StatusServiceError(var error) => error switch {
                ServiceError.NotFound or ServiceError.Unauthorized => Err<Unit, DeleteHandlerError>(
                    new DeleteHandlerError.NotFound()),
                _ => Err<Unit, DeleteHandlerError>(new DeleteHandlerError.Internal())
            },
            FsoStatus.Success => Ok<Unit, DeleteHandlerError>(new()),
            _ => throw new InvalidEnumArgumentException()
        };
    }

    private abstract record FsoStatus {
        public sealed record Success : FsoStatus;
        public sealed record ParseError : FsoStatus;
        public sealed record StatusServiceError(ServiceError Error) : FsoStatus;

        public static FsoStatus FromServiceResult(Result<Unit, ServiceError> result)
            => result switch {
                Err<Unit, ServiceError>(var err) => new StatusServiceError(err),
                Ok<Unit, ServiceError> => new Success(),
                _ => throw new InvalidEnumArgumentException()
            };
    }
}
