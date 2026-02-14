using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

using ZipZap.Classes;
using ZipZap.Classes.Helpers;
using ZipZap.Front.Factories;
using ZipZap.Front.Services;
using ZipZap.LangExt.Helpers;

using static ZipZap.LangExt.Helpers.ResultConstructor;

namespace ZipZap.Front.Handlers.Files.Create;

public class GetHandler {
    public static async Task<Result<GetHandler, GetHandlerError>> OnGetAsync(FsoId id, HttpRequest request, IFactory<IBackend, BackendConfiguration> backendFactory) {
        var token = request.Cookies[Constants.AUTHORIZATION];
        if (token is null)
            return Err<GetHandler, GetHandlerError>(new GetHandlerError.Unauthorized());
        var backend = backendFactory.Create(new(token));
        var fsoResult = await backend.GetFsoByIdAsync(id);
        if (fsoResult is Err<Fso, ServiceError>(var err)) {
            return err switch {
                ServiceError.NotFound => Err<GetHandler, GetHandlerError>(new GetHandlerError.NotFound()),
                ServiceError.Unauthorized => Err<GetHandler, GetHandlerError>(new GetHandlerError.Unauthorized()),
                _ => Err<GetHandler, GetHandlerError>(new GetHandlerError.HandlerServiceError(err))
            };
        }

        return Ok<GetHandler, GetHandlerError>(new());
    }

    public abstract record GetHandlerError {
        public sealed record NotFound : GetHandlerError;

        public sealed record Unauthorized : GetHandlerError;
        public sealed record HandlerServiceError(ServiceError Error) : GetHandlerError;
    }

}
