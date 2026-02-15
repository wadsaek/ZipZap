// Login.cshtml.cs - Part of the ZipZap project for storing files online
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

using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using ZipZap.Classes;
using ZipZap.Classes.Helpers;
using ZipZap.Front.Services;
using ZipZap.LangExt.Helpers;

namespace ZipZap.Front.Pages;

public class LoginModel : PageModel {
    private readonly ILoginService _loginService;

    public LoginModel(ILoginService loginService) {
        _loginService = loginService;
    }

    public User? ServiceUser { get; set; }
    public string Error { get; set; } = string.Empty;

    [BindProperty]
    public string Username { get; set; } = string.Empty;
    [BindProperty]
    public string Password { get; set; } = string.Empty;

    public void OnGet() {
    }

    public async Task OnPost() {
        var response = await _loginService.Login(Username, Password);
        if (response is Ok<string, LoginError>(var token)) {
            Error = string.Empty;
            Response.Cookies.Append(Constants.AUTHORIZATION, token);
        } else
            Error = response.ToString();
    }

}
