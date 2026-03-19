// Edit.cshtml.cs - Part of the ZipZap project for storing files online
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

using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

using ZipZap.Classes;
using ZipZap.Classes.Helpers;
using ZipZap.Front.Factories;
using ZipZap.Front.Handlers;
using ZipZap.Front.Handlers.Files.View;
using ZipZap.Front.Services;
using ZipZap.LangExt.Helpers;

namespace ZipZap.Front.Pages.Files;

using GetError = GetHandler.GetHandlerError;

public class Edit : PageModel {
    private readonly IBackendFactory _backendFactory;
    private readonly IGetHandler _getHandler;

    public Edit(ILogger<IGetHandler> logger, IBackendFactory backendFactory, IGetHandler getHandler) {
        _backendFactory = backendFactory;
        _getHandler = getHandler;
    }
    public GetHandler.GetHandlerResult? Result {
        get; set;
    }

    [BindProperty]
    public string Name { get; set; } = "";
    [BindProperty]
    public string Perms { get; set; } = "";
    [BindProperty]
    public int UId { get; set; }
    [BindProperty]
    public int GId { get; set; }

    public async Task<IActionResult> OnGetAsync(string path, [FromQuery] IdType type, CancellationToken cancellationToken) => await _getHandler.OnGetAsync(
            new(path, type),
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

    public async Task<IActionResult> OnPostAsync(string path, [FromQuery] IdType type, CancellationToken cancellationToken) {
        var handlerResult = await _getHandler.OnGetAsync(
            new(path, type),
            Request, cancellationToken
        );
        return await handlerResult
        .SelectAsync(async data => {
            Result = data;
            if (!Permissions.TryParse(Perms, out var permissions)) return FailWithError("Invalid permissions. Try 'rw-r--r--'");
            if (string.IsNullOrWhiteSpace(Name)) return FailWithError("Name is required");
            var fsData = Result.Item.Fso.Data with { Ownership = new(UId, GId), Name = Name, Permissions = permissions };
            var fso = Result.Item.Fso with { Data = fsData };
            var token = Request.Cookies[Constants.AUTHORIZATION];
            if (token is null)
                return Unauthorized();

            var backend = _backendFactory.Create(new(token));
            return await backend.UpdateFso(fso, cancellationToken)
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
