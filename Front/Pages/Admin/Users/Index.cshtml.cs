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

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using ZipZap.Classes;
using ZipZap.Classes.Helpers;
using ZipZap.Front.Factories;
using ZipZap.Front.Services;
using ZipZap.LangExt.Helpers;

namespace ZipZap.Front.Pages.Admin.Users;

public class IndexModel : PageModel {
    private readonly IBackendFactory _factory;

    public IndexModel(IBackendFactory factory) {
        _factory = factory;
    }
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken) {
        var token = Request.Cookies[Constants.AUTHORIZATION];
        if (token is null) return Redirect("/");
        var backend = _factory.Create(new(token));
        return await backend.AdminGetUsers(cancellationToken)
        .SelectAsync(users => {
            Users = users.ToImmutableList();
            return Page() as IActionResult;
        })
        .UnwrapOrElseAsync(err => err switch {
            ServiceError.Unauthorized => Redirect("/"),
            ServiceError.Unknown(var exception) => throw exception,
            _ => throw new System.NotImplementedException()
        });
    }
    public ImmutableList<User> Users { get; set; } = [];
}
