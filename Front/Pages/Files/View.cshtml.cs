// View.cshtml.cs - Part of the ZipZap project for storing files online
//     Copyright (C) 2026  Barenboim Esther wadsaek@gmail.com
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY, without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
//
//     You should have received a copy of the GNU General Public License
//     along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

using ZipZap.Classes;
using ZipZap.Classes.Extensions;
using ZipZap.Front.Factories;
using ZipZap.Front.Handlers;
using ZipZap.Front.Handlers.Files.View;
using ZipZap.Front.Services;
using ZipZap.LangExt.Helpers;

namespace ZipZap.Front.Pages.Files;

using DeleteError = DeleteHandler.DeleteHandlerError;
using GetError = GetHandler.GetHandlerError;

public class FileViewModel : PageModel {

    private readonly ILogger<FileViewModel> _logger;
    private readonly IBackendFactory _backendFactory;
    private readonly IRequestBackendFactory _factory;

    public FileViewModel(ILogger<FileViewModel> logger, IBackendFactory backendFactory, IGetHandler getHandler, IRequestBackendFactory factory) {
        _logger = logger;
        _backendFactory = backendFactory;
        _getHandler = getHandler;
        _factory = factory;
    }

    private readonly IGetHandler _getHandler;
    public static string FsoTypeName(Fso fso) => fso switch {
        Classes.File => "regular file",
        Directory => "directory",
        Symlink => "symbolic link",
        _ => throw new InvalidEnumArgumentException()
    };

    public async Task<IActionResult> OnGetAsync(string path, [FromQuery] IdType type, CancellationToken cancellationToken)
        => await _getHandler.OnGetAsync(
                new(path, type), Request, cancellationToken
            )
        .SelectAsync(result => {
            Result = result;
            return Page() as IActionResult;
        })
        .UnwrapOrElseAsync(err => err switch {
            GetError.ShouldRedirect(var target) => Redirect(target),
            GetError.BadRequest => BadRequest(),
            GetError.NotFound => NotFound(),
            _ => throw new InvalidEnumArgumentException()
        });

    public GetHandler.GetHandlerResult? Result { get; set; }

    public async Task<IActionResult> OnPostDelete([FromRoute] Guid path)
        => await DeleteHandler.OnDeleteAsync(path.ToFsoId(), Request, _backendFactory)
        .SelectAsync(_ => Redirect(Request.Headers.Referer.ToString()) as IActionResult)
        .UnwrapOrElseAsync(err => err switch {
            DeleteError.Internal => ReportInternalAndRedirect(),
            DeleteError.BadRequest => BadRequest(),
            DeleteError.NotFound => NotFound(),
            _ => throw new InvalidEnumArgumentException()
        });
    public async Task<IActionResult> OnPostUnshare(Guid accessId, CancellationToken cancellationToken) {

        var backend = _factory.TryGetFromRequest(Request);
        if (backend is null) return Redirect("/");

        return await backend.Unshare(accessId.ToFsoAccessId(), cancellationToken)
        .SelectAsync(_ => Redirect(Request.Headers.Referer.ToString()) as IActionResult)
        .UnwrapOrElseAsync(err => err switch {
            ServiceError.Unknown(var e) => ReportInternalAndRedirect(),
            ServiceError.BadRequest => BadRequest(),
            ServiceError.NotFound => NotFound(),
            ServiceError.Unauthorized => Unauthorized(),
            _ => ReportAndRedirect(err, Request)
        });
    }

    private RedirectResult ReportAndRedirect(ServiceError error, HttpRequest request) {

        if (_logger.IsEnabled(LogLevel.Critical))
            _logger.LogCritical("Weird error found: {Error}", error);
        return Redirect(request.Headers.Referer.ToString());

    }
    private RedirectResult ReportInternalAndRedirect() {

        if (_logger.IsEnabled(LogLevel.Critical))
            _logger.LogCritical("Internal error Found");
        return Redirect(".");

    }
}

