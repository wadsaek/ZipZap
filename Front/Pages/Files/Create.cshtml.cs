// Create.cshtml.cs - Part of the ZipZap project for storing files online
//     Copyright (C) 2026  Barenboim Esther wadsaek@gmail.com
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
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
    private readonly IFactory<IBackend, BackendConfiguration> _backendFactory;

    public CreateModel(ILogger<CreateModel> logger, IFactory<IBackend, BackendConfiguration> backendFactory) {
        _logger = logger;
        _backendFactory = backendFactory;
    }

    public async Task<IActionResult> OnGetAsync([FromQuery] Guid id) {
        return await GetHandler.OnGetAsync(id.ToFsoId(), Request, _backendFactory) switch {
            Err<GetHandler, Error>(Error.Unauthorized) => RedirectToPage("/"),
            Err<GetHandler, Error>(Error.HandlerServiceError) => RedirectToPage("/Error"),
            Err<GetHandler, Error>(Error.NotFound) => NotFound(),
            Ok<GetHandler, Error>(var handler) => SetupHandler(handler),
            _ => throw new InvalidEnumArgumentException()
        };
    }
    public GetHandler? GetHandler { get; private set; }

    private PageResult SetupHandler(GetHandler handler) {
        GetHandler = handler;
        return Page();
    }


    public async Task<IActionResult> OnPostAsync([FromQuery] Guid id) {
        if (!Permissions.TryParse(Perms, out var permissions)) {
            FormError = "Wrong permissions";
            return Page();
        }
        var data = new FsData(
            id.ToFsoId().AsIdOf<Directory>(),
            permissions,
            Name,
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
        return request switch {
            Ok<Fso, ServiceError>(var fso) => Redirect($"/Files/View/{fso.Id}?type=id"),
            Err<Fso, ServiceError>(var err) => err switch {
                ServiceError.BadRequest when FsoType == FsoType.RegularFile && File is null => HandleEmptyFile(),
                ServiceError.BadRequest => HandleBadRequest(),
                ServiceError.BadResult or ServiceError.Unknown => throw new("bad result encountered"),
                ServiceError.NotFound => HandleNotFound(),
                ServiceError.Unauthorized => Redirect("/"),
                ServiceError.AlreadyExists => HandleExists(),
                ServiceError.FailedPrecondition(var message) => HandleFailedPrecondition(message),
                _ => throw new InvalidEnumArgumentException()
            },
            _ => throw new InvalidEnumArgumentException()
        };
    }

    private PageResult HandleExists() {
        FormError = "This fso already exists. Try editing or using a different name!";
        return Page();
    }

    private PageResult HandleFailedPrecondition(string message) {
        FormError = message;
        return Page();
    }

    private PageResult HandleNotFound() {
        FormError = "Not Found";
        return Page();
    }

    private PageResult HandleBadRequest() {
        FormError = "Bad request";
        return Page();
    }

    private PageResult HandleEmptyFile() {
        FileError = "File not uploaded";
        return Page();
    }

    private static async Task<Result<Fso, ServiceError>> UploadDirectory(IBackend backend, FsData data)
        => (await backend.MakeDirectory(new(default, data))).Select(dir => dir as Fso);

    private async Task<Result<Fso, ServiceError>> UploadFile(IBackend backend, FsData data) {
        if (File is null) return Err<Fso, ServiceError>(new ServiceError.BadRequest());
        return (await backend.SaveFile(await ByteString.FromStreamAsync(File.OpenReadStream()), new(default, data))).Select(dir => dir as Fso);
    }

    private async Task<Result<Fso, ServiceError>> UploadSymlink(IBackend backend, FsData data)
        => (await backend.MakeLink(new(default, data, Target))).Select(dir => dir as Fso);

    [BindProperty]
    [Display(Name = "Fso Type")]
    [Required]
    public FsoType FsoType { get; set; } = FsoType.RegularFile;
    [BindProperty]
    [Required]
    public string Name { get; set; } = string.Empty;
    [BindProperty]
    [Required]
    [Display(Name = "User Id")]
    public int UId { get; set; } = 1000;
    [BindProperty]
    [Required]
    [Display(Name = "Group Id")]
    public int GId { get; set; } = 100;
    [BindProperty]
    [Required]
    [Display(Name = "UNIX Permissions")]
    public string Perms { get; set; } = Permissions.FileDefault.ToString();
    [BindProperty]
    [Display(Name = "Symlink Target")]
    public string Target { get; set; } = "";
    [BindProperty]
    [Display(Name = "File")]
    public new IFormFile? File { get; set; }

    public string FileError { get; set; } = string.Empty;
    public string FormError { get; set; } = string.Empty;
}
