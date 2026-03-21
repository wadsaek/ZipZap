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

using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

using ZipZap.Classes;
using ZipZap.Classes.Helpers;
using ZipZap.Front.Factories;
using ZipZap.Front.Services;
using ZipZap.LangExt.Helpers;

namespace ZipZap.Front.Pages;

public class IndexModel : PageModel {
    private readonly IBackendFactory _backendFactory;
    private readonly ILogger<IndexModel> _logger;
    private readonly ILoginService _loginService;

    public IndexModel(IBackendFactory backendFactory, ILoginService loginService, ILogger<IndexModel> logger) {
        _backendFactory = backendFactory;
        _loginService = loginService;
        _logger = logger;
    }
    public new User? User { get; set; }
    public (FsoAccess acc, User u)[] Accessible = [];
    public FsoAccess[] Shared = [];

    public async Task<IActionResult> OnGet(CancellationToken cancellationToken) {
        var token = Request.Cookies[Constants.AUTHORIZATION];
        if (token is null)
            return Page();

        var backend = _backendFactory.Create(new(token));
        User = await backend.GetSelf(cancellationToken).UnwrapAsync();
        var shares = await backend.GetAccessible(cancellationToken).UnwrapOrAsync([]);
        var tasks = shares.Select(async acc => {
            var owner = await backend.GetFsoOwner(acc.Fso.Id, cancellationToken);
            return owner.Select(u => (acc, u));
        });
        var results = await Task.WhenAll(tasks);
        var result = results.ToArrayResult();
        return await result
        .SelectManyAsync(accesible => backend.GetSharedBySelf(cancellationToken).SelectAsync(shared => (shared.ToArray(), accesible)))
        .SelectAsync(accs => {
            (Shared, Accessible) = accs;
            return Page();
        })
        .UnwrapOrElseAsync(e => {
            if (_logger.IsEnabled(LogLevel.Critical))
                _logger.LogCritical("Received an error when getting owners for fso data: {Err}", e);
            return Page();
        });
    }

    public async Task<IActionResult> OnPost(CancellationToken cancellationToken) {
        var response = await _loginService.Login(Username, Password, cancellationToken);
        if (response is Ok<string, LoginError>(var token)) {
            Error = null;
            Response.Cookies.Append(Constants.AUTHORIZATION, token);
            return Redirect(Request.Path);
        }

        if (response is Err<string, LoginError>(var error)) {
            Error = error.ToString();
        }
        return Page();
    }

    public string? Error { get; set; }

    [Required]
    [BindProperty]
    public string Username { get; set; } = string.Empty;

    [Required]
    [BindProperty]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}
