using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using ZipZap.Classes;
using ZipZap.Classes.Extensions;
using ZipZap.Classes.Helpers;
using static ZipZap.Classes.Helpers.Constructors;
using ZipZap.Front.Factories;
using ZipZap.Front.Services;

namespace ZipZap.Front.Handlers.Files.View;

public class GetHandler {
    public Fso Item { get; }
    public string? Text { get; }
    public string ParentUrl { get; }
    public FileSpecification Specification { get; }

    public abstract record GetHandlerError {
        public sealed record NotFound : GetHandlerError;
        public sealed record BadRequest : GetHandlerError;
        public sealed record ShouldRedirect(string Target) : GetHandlerError;

    }
    public GetHandler(Fso item,
                      string? text,
                      string parentUrl,
                      FileSpecification specification
                  ) {
        Item = item;
        Text = text;
        ParentUrl = parentUrl;
        Specification = specification;
    }

    public static async Task<Result<GetHandler, GetHandlerError>> OnGetAsync(FileSpecification specification, HttpRequest request, ILogger<GetHandler> logger, IFactory<IBackend, BackendConfiguration> backendFactory) {
        if (specification.Type == IdType.Path)
            specification = specification with { Identifier = specification.Identifier?.NormalizePath() };
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Path: {path}\nType: {type}", specification.Identifier, specification.Type);
        var token = request.Cookies[Constants.AUTHORIZATION];
        if (token is null)
            return Err<GetHandler, GetHandlerError>(new GetHandlerError.NotFound());

        var backend = backendFactory.Create(new(token));
        var status = await GetFsoBySpecificationAsync(specification, backend);

        if (status is FsoStatus.ParseError) return Err<GetHandler, GetHandlerError>(new GetHandlerError.BadRequest());
        if (status is FsoStatus.StatusServiceError)
            return Err<GetHandler, GetHandlerError>(new GetHandlerError.ShouldRedirect(GetRedirectUrl(specification, null)));

        var item = (status as FsoStatus.Success)!.Fso;
        var parentUrl = GetRedirectUrl(specification, item);
        string? text = null;

        if (item is Symlink { Target: var target })
            return Err<GetHandler, GetHandlerError>(new GetHandlerError.ShouldRedirect(target));
        if (item is File file) {
            text = Encoding.UTF8.GetString(file.Content ?? []);
        }
        return Ok<GetHandler, GetHandlerError>(new(item, text, parentUrl, specification));
    }


    public string GetChildLink(Fso child) => "/Files/View/" + Specification switch {
        { Type: IdType.Id } => $"{child.Id}?type=id",
        { Type: IdType.Path, Identifier: "" } => $"{child.Data.Name}",
        { Type: IdType.Path, Identifier: var path } => $"{path}/{child.Data.Name}",
        { Type: _ } => throw new InvalidEnumArgumentException()
    };


    public static string GetParentDir(string path) {
        path = "/" + path.NormalizePath();
        return "/Files/View" + path[..path.LastIndexOf('/')];
    }

    private static async Task<FsoStatus> GetFsoBySpecificationAsync(FileSpecification specification, IBackend backend) {
        if (specification.Type == IdType.Id)
            return await Shared.GetFsoById(backend, specification.Identifier);

        return FsoStatus.FromServiceResult(await backend.GetFsoByPathAsync(
            PathData.CreatePathDataWithPath(specification.Identifier)
        ));
    }

    private static string GetRedirectUrl(FileSpecification specification, Fso? fso)
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
