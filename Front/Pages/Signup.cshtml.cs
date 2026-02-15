// Signup.cshtml.cs - Part of the ZipZap project for storing files online
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
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using ZipZap.Classes.Helpers;
using ZipZap.Front.Services;
using ZipZap.LangExt.Helpers;

using Errs = ZipZap.Front.Services.SignupError;

namespace ZipZap.Front;

public class Signup : PageModel {
    private readonly ILoginService _loginService;

    public Signup(ILoginService loginService) {
        _loginService = loginService;
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken) {
        var result = await _loginService.SignUp(new(
            Username: Username,
            Password: Password,
            Email: Email,
            new(Uid, Gid)),
            cancellationToken
        );
        return result switch {
            Ok<string, Errs>(var token) => HandleToken(token),
            Err<string, Errs>(var err) => err switch {
                Errs.EmptyCredentials => ReturnError("One or more fields is empty"),
                Errs.InvalidEmail => ReturnError("Your email is invalid"),
                Errs.InvalidLogin => ReturnError("Your login is invalid"),
                Errs.InvalidPassword => ReturnError("Your password is invalid"),
                Errs.UserExists => ReturnError("User with this username already exists"),
                _ => ReturnError(err.ToString())
            },
            _ => throw new InvalidEnumArgumentException()
        };
    }

    private RedirectResult HandleToken(string token) {
        Error = null;
        Response.Cookies.Append(Constants.AUTHORIZATION, token);
        return Redirect("/");
    }

    private PageResult ReturnError(string v) {
        Error = v;
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
    [Required]
    [BindProperty]
    [DataType(DataType.EmailAddress)]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    [DisplayName("Default group ID")]
    public int Gid { get; set; } = 100;
    [BindProperty]
    [DisplayName("Default user ID")]
    public int Uid { get; set; } = 1000;
}
