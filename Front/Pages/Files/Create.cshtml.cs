// Create.cshtml.cs - Part of the ZipZap project for storing files online
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
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;

using Google.Protobuf;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

using ZipZap.Classes;
using ZipZap.Classes.Extensions;
using ZipZap.Classes.Helpers;
using ZipZap.Front.Factories;
using ZipZap.Front.Handlers.Files.Create;
using ZipZap.Front.Services;
using ZipZap.LangExt.Helpers;

using static ZipZap.LangExt.Helpers.ResultConstructor;
namespace ZipZap.Front.Pages.Files;

using Error = GetHandler.GetHandlerError;
public class CreateModel : PageModel {
    private readonly ILogger<CreateModel> _logger;
    private readonly IBackendFactory _backendFactory;

    public CreateModel(ILogger<CreateModel> logger, IBackendFactory backendFactory) {
        _logger = logger;
        _backendFactory = backendFactory;
    }

    public Task<IActionResult> OnGetAsync([FromQuery] Guid id, CancellationToken cancellationToken)
        => OnGetAsyncRaw(id.ToFsoId(), cancellationToken);
    public async Task<IActionResult> OnGetAsyncRaw(FsoId id, CancellationToken cancellationToken) {
        return await GetHandler.OnGetAsync(id, Request, _backendFactory, cancellationToken)
        .SelectAsync(handler => {
            GetHandler = handler;
            (UId, GId) = handler.User.DefaultOwnership;
            return Page() as IActionResult;
        })
        .UnwrapOrElseAsync(err => err switch {
            Error.Unauthorized => RedirectToPage("/"),
            Error.HandlerServiceError => RedirectToPage("/Error"),
            Error.NotFound => NotFound(),
            _ => throw new InvalidEnumArgumentException()
        });

    }
    public GetHandler? GetHandler { get; private set; }

    public async Task<IActionResult> OnPostAsync([FromQuery] Guid id, CancellationToken cancellationToken) {
        Permissions permissions;
        bool success;
        if (Perms is not null) {
            success = Permissions.TryParse(Perms, out permissions);
        } else (success, permissions) = FsoType switch {
            FsoType.RegularFile => (true, Permissions.FileDefault),
            FsoType.Symlink => (true, Permissions.SymlinkDefault),
            FsoType.Directory => (true, Permissions.DirectoryDefault),
            _ => (false, default)
        };
        if (!success) {
            FormError = "Wrong permissions";
            return Page();
        }
        var fsoId = id.ToFsoId();
        var data = new FsData(
            fsoId.AsIdOf<Directory>(),
            permissions,
            Name ?? string.Empty,
            new(UId, GId)
        );

        var token = Request.Cookies[Constants.AUTHORIZATION];
        if (token is null) return Redirect("/");
        var backend = _backendFactory.Create(new(token));
        var request = FsoType switch {
            FsoType.Symlink => await UploadSymlink(backend, data),
            FsoType.RegularFile => await UploadFile(backend, data),
            FsoType.Directory => await UploadDirectory(backend, data),
            _ => throw new InvalidEnumArgumentException()
        };
        return await request
        .Select(fso => Redirect($"/Files/View/{fso.Id}?type=id") as IActionResult)
        .UnwrapOrElseAsync(async err => err switch {
            ServiceError.BadRequest(var detail) => await HandleBadRequest(detail, fsoId, cancellationToken),
            ServiceError.BadResult or ServiceError.Unknown => throw new("bad result encountered"),
            ServiceError.NotFound => await HandleNotFound(fsoId, cancellationToken),
            ServiceError.Unauthorized => Redirect("/"),
            ServiceError.AlreadyExists => await HandleExists(fsoId, cancellationToken),
            ServiceError.FailedPrecondition(var message) => await HandleFailedPrecondition(message, fsoId, cancellationToken),
            _ => throw new InvalidEnumArgumentException()
        });
    }

    private Task<IActionResult> HandleExists(FsoId id, CancellationToken cancellationToken) {
        FormError = "This fso already exists. Try editing or using a different name!";
        return OnGetAsync(id.Value, cancellationToken);
    }

    private Task<IActionResult> HandleFailedPrecondition(string message, FsoId id, CancellationToken cancellationToken) {
        FormError = message;
        return OnGetAsyncRaw(id, cancellationToken);
    }

    private Task<IActionResult> HandleNotFound(FsoId id, CancellationToken cancellationToken) {
        FormError = "Not Found";
        return OnGetAsyncRaw(id, cancellationToken);
    }

    private Task<IActionResult> HandleBadRequest(string detail, FsoId id, CancellationToken cancellationToken) {
        FormError = detail;
        return OnGetAsyncRaw(id, cancellationToken);
    }

    private static async Task<Result<Fso, ServiceError>> UploadDirectory(IBackend backend, FsData data)
        => (await backend.MakeDirectory(new(default, data))).Select(dir => dir as Fso);

    private async Task<Result<Fso, ServiceError>> UploadFile(IBackend backend, FsData data) {
        if (File is null) return Err<Fso, ServiceError>(new ServiceError.BadRequest("File is empty"));
        return (await backend.SaveFile(await ByteString.FromStreamAsync(File.OpenReadStream()), new(default, data))).Select(dir => dir as Fso);
    }

    private async Task<Result<Fso, ServiceError>> UploadSymlink(IBackend backend, FsData data) {
        if (string.IsNullOrWhiteSpace(Target))
            return Err<Fso, ServiceError>(new ServiceError.BadRequest("Symlink target cannot be empty"));
        var result = await backend.MakeLink(new(default, data, Target));
        return result.Select(dir => dir as Fso);
    }

    [BindProperty]
    [Display(Name = "Fso Type")]
    [Required]
    public FsoType FsoType { get; set; } = FsoType.RegularFile;
    [BindProperty]
    [Required]
    public string? Name { get; set; }
    [BindProperty]
    [Display(Name = "User Id")]
    public int UId { get; set; } = 1000;
    [BindProperty]
    [Required]
    [Display(Name = "Group Id")]
    public int GId { get; set; } = 100;
    [BindProperty]
    [Required]
    [Display(Name = "UNIX Permissions")]
    public string? Perms { get; set; } = null;
    [BindProperty]
    [Display(Name = "Symlink Target")]
    public string? Target { get; set; }
    [BindProperty]
    [Display(Name = "File")]
    public new IFormFile? File { get; set; }

    public string FormError { get; set; } = string.Empty;
}
