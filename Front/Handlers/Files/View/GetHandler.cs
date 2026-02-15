// GetHandler.cs - Part of the ZipZap project for storing files online
//     Copyright (C) 2026  Barenboim Esther wadsaek@gmail.com
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using ZipZap.Classes;
using ZipZap.Classes.Extensions;
using ZipZap.Classes.Helpers;
using ZipZap.Front.Services;
using ZipZap.LangExt.Extensions;
using ZipZap.LangExt.Helpers;

using static ZipZap.LangExt.Helpers.ResultConstructor;

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

        if (item is Symlink link)
            return Err<GetHandlerResult, GetHandlerError>(new GetHandlerError.ShouldRedirect(await GetSymlinkLink(specification, backendConfiguration, link, cancellationToken)));
        if (item is File file) {
            text = Encoding.UTF8.GetString(file.Content ?? []);
        }
        return Ok<GetHandlerResult, GetHandlerError>(new(item, text, parentUrl, specification));
    }

    private async Task<string> GetSymlinkLink(FileSpecification specification, BackendConfiguration backendConfiguration, Symlink link, CancellationToken cancellationToken) {
        // the browser can handle those just fine
        if (specification.Type == IdType.Path) return link.Target;

        if (await _service.GetFullPath(link.Id, backendConfiguration, cancellationToken) is not Ok<IEnumerable<string>, ServiceError>(var path)) return "";
        var parts = path.ToList();
        // We're interested in the directory, not the actual path
        parts.RemoveAt(parts.Count - 1);
        var targetParts = link.Target.SplitPath();
        foreach (var part in targetParts) {
            switch (part) {
                case "." or "":
                    break;
                case "..":
                    parts.RemoveAt(parts.Count - 1);
                    break;
                case var p:
                    parts.Add(p);
                    break;
            }
        }
        var result = await _service.GetFsoWithRoot(
            new PathDataWithPath(parts.ConcatenateWith("/")),
            link.Id,
            backendConfiguration,
            cancellationToken);
        return result switch {
            FsoStatus.Success(var fso) => $"/Files/View/{fso.Id}?type=id",
            _ => "."
        };
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
