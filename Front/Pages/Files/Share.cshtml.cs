// Share.cshtml.cs - Part of the ZipZap project for storing files online
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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

using ZipZap.Classes;
using ZipZap.Classes.Extensions;
using ZipZap.Front.Factories;
using ZipZap.Front.Handlers;
using ZipZap.Front.Services;
using ZipZap.LangExt.Helpers;

namespace ZipZap.Front.Pages.Files;

public class ShareModel : PageModel {
    private readonly IRequestBackendFactory _factory;
    private readonly ILogger<ShareModel> _logger;

    public ShareModel(IRequestBackendFactory factory, ILogger<ShareModel> logger) {
        _factory = factory;
        _logger = logger;
    }

    public Fso? Item { get; private set; }
    public List<User> Users { get; private set; } = [];

    [BindProperty]
    public string? Username { get; set; } = "";

    public string? Error { get; set; } = null;

    public Task<IActionResult> OnGet(Guid id, CancellationToken cancellationToken) {
        return OnGetRaw(id.ToFsoId(), cancellationToken);
    }
    public Task<IActionResult> OnGetRaw(FsoId id, CancellationToken cancellationToken) {
        var backend = _factory.TryGetFromRequest(Request);
        if (backend is null) return Task.FromResult(Redirect("/") as IActionResult);
        return backend.GetFsoByIdAsync(id, cancellationToken)
        .SelectManyAsync(async fso => {
            var result = await backend.GetSharedBySelf(cancellationToken);
            return result
                .Select(accesses =>
                    accesses
                        .Select(a => a.User)
                        .DistinctBy(a => a.Id)
                        .ToList()
                )
                .Select(users => (users, fso));
        })
        .SelectAsync(param => {
            (Users, Item) = param;
            return Page() as IActionResult;
        })
        .UnwrapOrElseAsync(err => err switch {
            ServiceError.NotFound => Redirect(Request.Headers.Referer.ToString()),
            ServiceError.Unauthorized => Redirect("/"),
            _ => HandleError(err)
        });
    }
    public Task<IActionResult> OnPost(Guid id, CancellationToken cancellationToken) {
        var backend = _factory.TryGetFromRequest(Request);
        if (backend is null) return Task.FromResult(Redirect("/") as IActionResult);
        var fsoId = id.ToFsoId();
        if (string.IsNullOrWhiteSpace(Username)) {
            return SetErrorPost("Username cannot be empty", fsoId, cancellationToken);
        }
        return backend.ShareFsoByUsername(fsoId, Username, cancellationToken)
        .SelectAsync(_ => RedirectToPage("/Files/View", new { Path = id.ToString(), Type = IdType.Id }) as IActionResult)
        .UnwrapOrElseAsync(async err => err switch {
            ServiceError.AlreadyExists => await SetErrorPost("This fso was already shared with the user", fsoId, cancellationToken),
            ServiceError.NotFound => Redirect(Request.Headers.Referer.ToString()),
            ServiceError.Unauthorized => Redirect("/"),
            _ => HandleError(err)
        });
    }

    private RedirectResult HandleError(ServiceError e) {
        if (_logger.IsEnabled(LogLevel.Critical))
            _logger.LogCritical("Service error encountered: {Error}", e);
        return Redirect("/");
    }
    private Task<IActionResult> SetErrorPost(string error, FsoId id, CancellationToken cancellationToken) {
        Error = error;
        return OnGetRaw(id, cancellationToken);
    }
}
