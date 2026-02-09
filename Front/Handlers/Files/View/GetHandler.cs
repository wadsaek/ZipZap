using System.ComponentModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using ZipZap.Classes;
using ZipZap.Classes.Extensions;
using ZipZap.Classes.Helpers;
using static ZipZap.LangExt.Helpers.ResultConstructor;
using ZipZap.Front.Services;
using ZipZap.LangExt.Helpers;

namespace ZipZap.Front.Handlers.Files.View;

public interface IGetHandler {
    string GetParentDir(string path);
    string GetRedirectUrl(FileSpecification specification, Fso? fso);
    Task<Result<GetHandler.GetHandlerResult, GetHandler.GetHandlerError>> OnGetAsync(FileSpecification specification, HttpRequest request, CancellationToken cancellationToken = default);
}

public class GetHandler : IGetHandler {
    private readonly ILogger<GetHandler> _logger;
    private readonly IFsoService _service;

    public GetHandler(ILogger<GetHandler> logger, IFsoService service) {
        _logger = logger;
        _service = service;
    }

    public abstract record GetHandlerError {
        public sealed record NotFound : GetHandlerError;

        public sealed record BadRequest : GetHandlerError;

        public sealed record ShouldRedirect(string Target) : GetHandlerError;

    }

    public record GetHandlerResult(Fso Item, string? Text, string ParentUrl, FileSpecification Specification);

    public async Task<Result<GetHandlerResult, GetHandlerError>> OnGetAsync(FileSpecification specification, HttpRequest request, CancellationToken cancellationToken = default) {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Path: {path}\nType: {type}", specification.Identifier, specification.Type);
        var token = request.Cookies[Constants.AUTHORIZATION];
        if (token is null)
            return Err<GetHandlerResult, GetHandlerError>(new GetHandlerError.NotFound());

        var backendConfiguration = new BackendConfiguration(token);
        var status = await _service.GetFsoBySpecificationAsync(specification, backendConfiguration, cancellationToken);

        if (status is FsoStatus.ParseError) return Err<GetHandlerResult, GetHandlerError>(new GetHandlerError.BadRequest());
        if (status is FsoStatus.StatusServiceError)
            return Err<GetHandlerResult, GetHandlerError>(new GetHandlerError.ShouldRedirect(GetRedirectUrl(specification, null)));

        var item = (status as FsoStatus.Success)!.Fso;
        var parentUrl = GetRedirectUrl(specification, item);
        string? text = null;

        if (item is Symlink { Target: var target })
            return Err<GetHandlerResult, GetHandlerError>(new GetHandlerError.ShouldRedirect(target));
        if (item is File file) {
            text = Encoding.UTF8.GetString(file.Content ?? []);
        }
        return Ok<GetHandlerResult, GetHandlerError>(new(item, text, parentUrl, specification));
    }



    public string GetParentDir(string path) {
        path = "/" + path.NormalizePath();
        return "/Files/View" + path[..path.LastIndexOf('/')];
    }


    public string GetRedirectUrl(FileSpecification specification, Fso? fso)
        => specification switch {
            { Type: IdType.Path, Identifier: null } => "/Files/View/",
            { Type: IdType.Path, Identifier: string path } => GetParentDir(path),
            { Type: IdType.Id } => fso switch {
                { Data.VirtualLocation.Id: var id } => $"/Files/View/{id}?type=id",
                null or { Data.VirtualLocation: null } => "/Files/View/",
            },
            { Type: _ } => throw new InvalidEnumArgumentException()
        };

}
public static class GetHandlerResultExt {
    extension(GetHandler.GetHandlerResult result) {
        private string GetChildPartialLink(Fso child) => result.Specification switch {
            { Type: IdType.Id } => $"{child.Id}?type=id",
            { Type: IdType.Path, Identifier: "" } => $"{child.Data.Name}",
            { Type: IdType.Path, Identifier: var path } => $"{path}/{child.Data.Name}",
            { Type: _ } => throw new InvalidEnumArgumentException()
        };
        public string GetChildLink(Fso child) => "/Files/View/" + result.GetChildPartialLink(child);
        public string GetEditLink() => "/Files/Edit/" + result.GetSelfFragment();
        public string GetViewLink() => "/Files/View/" + result.GetSelfFragment();
        public string GetSelfFragment() => result.Specification switch {
            { Type: IdType.Id, Identifier: var id } => $"{id}?type=id",
            { Type: IdType.Path, Identifier: var path } => path ?? "",
            { Type: _ } => throw new InvalidEnumArgumentException()
        };
    }
}
