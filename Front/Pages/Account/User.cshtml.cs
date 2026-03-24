// User.cshtml.cs - Part of the ZipZap project for storing files online
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
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

using ZipZap.Classes.Extensions;
using ZipZap.Classes.Helpers;
using ZipZap.Front.Factories;
using ZipZap.Front.Handlers.Users;
using ZipZap.Front.Services;
using ZipZap.LangExt.Helpers;

using static ZipZap.LangExt.Helpers.ResultConstructor;

namespace ZipZap.Front.Pages.Account;

using static IGetHandler;

public class UserModel : PageModel {
    private readonly IGetHandler _handler;
    private readonly IRequestBackendFactory _factory;
    private readonly ILogger<UserModel> _logger;

    public UserModel(IGetHandler handler, IRequestBackendFactory factory, ILogger<UserModel> logger) {
        _handler = handler;
        _factory = factory;
        _logger = logger;
    }

    public IResult? HandlerResult { get; private set; }
    public bool ShowEmail { get; private set; }

    public async Task<IActionResult> OnGet(Guid? id, CancellationToken cancellationToken) {
        return await _handler.Get(Request, id?.ToUserId(), cancellationToken)
        .SelectAsync(res => {
            HandlerResult = res;
            ShowEmail = res.DeleteUser is not null;
            return Page() as IActionResult;
        })
        .UnwrapOrElseAsync(err => err switch {
            Error.ShouldRedirect(var target) => Redirect(target),
            Error.NotFound => NotFound(),
            _ => Redirect("/")
        });
    }
    public async Task<IActionResult> OnPostDelete(Guid? id, CancellationToken cancellationToken) {
        return await _handler.Get(Request, id?.ToUserId(), cancellationToken)
        .SelectManyAsync(res => res.DeleteUser?.Invoke(cancellationToken) ?? Task.FromResult(Ok<Unit, Error>(new())))
        .SelectAsync(_ => {
            Response.Cookies.Delete(Constants.AUTHORIZATION);
            return Redirect("..");
        })
        .UnwrapOrElseAsync(err => err switch {
            Error.ShouldRedirect(var target) => Redirect(target),
            _ => Redirect("/")
        });
    }
    public Task<IActionResult> OnPostDeleteKey(Guid? id, [FromQuery(Name = "key-id")] Guid keyId, CancellationToken cancellationToken) {
        var backend = _factory.TryGetFromRequest(Request);
        if (backend is null) return Task.FromResult(Unauthorized() as IActionResult);
        return backend.RemoveSshKey(new(keyId), cancellationToken)
        .SelectAsync(_ => Redirect(Request.Path) as IActionResult)
        .UnwrapOrElseAsync(async err => err switch {
            ServiceError.Unauthorized => await SetError("You are unauthorized to delete this key", id, cancellationToken),
            ServiceError.NotFound => await SetError("Key not found", id, cancellationToken),
            _ => await SetErrorAndLog("Weird error encountered. Try again later", err, id, cancellationToken),
        });
    }

    private Task<IActionResult> SetErrorAndLog(string message, ServiceError error, Guid? id, CancellationToken cancellationToken) {
        KeyError = message;
        if (_logger.IsEnabled(LogLevel.Critical))
            _logger.LogCritical("Encountered a weird error: {Error}", error);
        return OnGet(id, cancellationToken);
    }

    private Task<IActionResult> SetError(string error, Guid? id, CancellationToken cancellationToken) {
        KeyError = error;
        return OnGet(id, cancellationToken);
    }

    public async Task<IActionResult> OnPostAddKey(Guid? id, CancellationToken cancellationToken) {
        if (string.IsNullOrEmpty(Key)) return await OnGet(id, cancellationToken);
        return await _handler.Get(Request, id?.ToUserId(), cancellationToken)
        .SelectManyAsync(res => res.AddSshKey?.Invoke(new(Key), cancellationToken) ?? Task.FromResult(Ok<Unit, Error>(new())))
        .SelectAsync(_ => Redirect(Request.Path))
        .UnwrapOrElseAsync(err => err switch {
            Error.ShouldRedirect(var target) => Redirect(target),
            _ => Redirect("/")
        });
    }
    [BindProperty]
    public string? Key { get; set; }
    public string? KeyError { get; private set; }
}
