// Index.cshtml.cs - Part of the ZipZap project for storing files online
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using ZipZap.Front.Factories;
using ZipZap.Front.Handlers.Settings;
using ZipZap.Front.Services;
using ZipZap.LangExt.Helpers;

using static ZipZap.Front.Handlers.Settings.IGetHandler;

namespace ZipZap.Front.Pages.Admin.Settings;

public class IndexModel : PageModel {
    public HandlerResult? Result { get; set; }
    private readonly IGetHandler _getHandler;
    private readonly IRequestBackendFactory _factory;

    public IndexModel(IGetHandler handler, IRequestBackendFactory factory) {
        _getHandler = handler;
        _factory = factory;
    }

    public async Task<IActionResult> OnGet(CancellationToken cancellationToken) {
        return await _getHandler.Handle(Request, cancellationToken)
        .SelectAsync(result => {
            Result = result;
            return Page() as IActionResult;
        })
        .UnwrapOrElseAsync(err => err switch {
            HandlerError.ShouldRedirect(var target) => Redirect(target) as IActionResult,
            _ => throw new InvalidEnumArgumentException()
        });
    }
    public async Task<IActionResult> OnPostAddKey(CancellationToken cancellationToken) {
        if (new[] { Key, ServerName }.Any(string.IsNullOrWhiteSpace))
            return await HandleError("Either the key or the server name is empty", cancellationToken);

        var backend = _factory.TryGetFromRequest(Request);
        if (backend is null) return Redirect("/");
        return await backend.AdminAddSshHostKey(new(Key), ServerName, cancellationToken)
        .SelectAsync(_ => RedirectToPage(this) as IActionResult)
        .UnwrapOrElseAsync(async err => err switch {
            ServiceError.AlreadyExists => await HandleError("This key already exists", cancellationToken),
            ServiceError.Unauthorized => Redirect("/"),
            ServiceError.BadRequest => await HandleError("This key format is unknown", cancellationToken),
            _ => throw new Exception(err.ToString())
        });
    }
    public async Task<IActionResult> OnPostDelete([FromQuery] Guid id, CancellationToken cancellationToken) {
        var backend = _factory.TryGetFromRequest(Request);
        if (backend is null) return Redirect("/");
        return await backend.AdminRemoveTrustedKey(new(id), cancellationToken)
        .SelectAsync(_ => RedirectToPage(this) as IActionResult)
        .UnwrapOrElseAsync(async err => err switch {
            ServiceError.NotFound => await HandleError("Weird. This key wasn't found", cancellationToken),
            ServiceError.Unauthorized => Redirect("/"),
            _ => throw new Exception(err.ToString())
        });
    }

    private Task<IActionResult> HandleError(string v, CancellationToken cancellationToken) {
        Error = v;
        return OnGet(cancellationToken);
    }

    public string? Error { get; set; } = null;
    [BindProperty]
    [DisplayName("Key")]
    public string Key { get; set; } = string.Empty;
    [BindProperty]
    [DisplayName("Server Name")]
    public string ServerName { get; set; } = string.Empty;
}
