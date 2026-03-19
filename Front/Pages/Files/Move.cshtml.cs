// Move.cshtml.cs - Part of the ZipZap project for storing files online
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

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

using ZipZap.Classes;
using ZipZap.Classes.Extensions;
using ZipZap.Classes.Helpers;
using ZipZap.Front.Factories;
using ZipZap.Front.Handlers;
using ZipZap.Front.Handlers.Files.View;
using ZipZap.Front.Services;
using ZipZap.LangExt.Helpers;

namespace ZipZap.Front.Pages.Files;

using GetError = GetHandler.GetHandlerError;

public class Move : PageModel {
    private readonly IRequestBackendFactory _backendFactory;
    private readonly IGetHandler _getHandler;
    private readonly IFsoService _fsoService;

    public Move(ILogger<IGetHandler> logger, IRequestBackendFactory backendFactory, IGetHandler getHandler, IFsoService fsoService) {
        _backendFactory = backendFactory;
        _getHandler = getHandler;
        _fsoService = fsoService;
    }
    public GetHandler.GetHandlerResult? Result {
        get; set;
    }

    [BindProperty]
    public string? NewPath { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken) => await _getHandler.OnGetAsync(
            new(id.ToString(), IdType.Id),
            Request, cancellationToken
        ) switch {
            Ok<GetHandler.GetHandlerResult, GetError>(var handler) => SetupHandler(handler),
            Err<GetHandler.GetHandlerResult, GetError>(GetError.ShouldRedirect(var target)) => Redirect(target),
            Err<GetHandler.GetHandlerResult, GetError>(GetError.BadRequest) => BadRequest(),
            Err<GetHandler.GetHandlerResult, GetError>(GetError.NotFound) => NotFound(),
            _ => throw new InvalidEnumArgumentException()

        };

    private PageResult SetupHandler(GetHandler.GetHandlerResult? handler) {
        Result = handler;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken cancellationToken) {
        var handlerResult = await _getHandler.OnGetAsync(
            new(id.ToString(), IdType.Id),
            Request, cancellationToken
        );
        return await handlerResult
        .SelectAsync(async data => {
            Result = data;
            if (string.IsNullOrWhiteSpace(NewPath)) return FailWithError("Path is required");

            var backend = _backendFactory.TryGetFromRequest(Request);
            if (backend is null) return FailWithError("Unauthorized");

            return await _fsoService.Move(data.Item, NewPath, backend, cancellationToken)
            .SelectAsync(_ => {

                Error = "";
                return Redirect(Result.ParentUrl) as IActionResult;
            })
            .UnwrapOrElseAsync(err2 => err2 switch {
                ServiceError.BadRequest => FailWithError("Bad Request"),
                ServiceError.BadResult => FailWithError("Bad Result"),
                ServiceError.FailedPrecondition or ServiceError.Unknown => FailWithError("Internal Server Error"),
                ServiceError.NotFound => FailWithError("Not Found"),
                ServiceError.Unauthorized => FailWithError("Unauthorized"),
                _ => throw new InvalidEnumArgumentException()
            });
        }).UnwrapOrElseAsync(err => err switch {
            GetError.ShouldRedirect(var target) => Redirect(target),
            GetError.BadRequest => BadRequest(),
            GetError.NotFound => NotFound(),
            _ => throw new InvalidEnumArgumentException()
        });
    }

    public string Error { get; set; } = "";

    private PageResult FailWithError(string error) {
        Error = error;
        return Page();
    }
}
