// Program.cs - Part of the ZipZap project for storing files online
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

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using ZipZap.Classes;
using ZipZap.Front.Factories;
using ZipZap.Front.Handlers.Exceptions;
using ZipZap.Front.Handlers.Files.View;
using ZipZap.Front.Services;

namespace ZipZap.Front;

public class Program {
    public static void Main(string[] args) {

        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddRazorPages();
        builder.Services.AddGrpcClient<Grpc.FilesStoringService.FilesStoringServiceClient>(options => {
            options.Address = new("http://localhost:5210");
            options.ChannelOptionsActions.Add(chOptions
                    => chOptions.MaxReceiveMessageSize = (int)FileSize.FromMegaBytes(16).Bytes);
        });
        builder.Services.AddScoped<IFactory<IBackend, BackendConfiguration>, BackendFactory>();
        builder.Services.AddScoped(_ => ServiceExceptionHandler.GetExceptionConverter());
        builder.Services.AddScoped<ILoginService, LoginService>();
        builder.Services.AddScoped<IFsoService, FsoService>();
        builder.Services.AddScoped<IGetHandler, GetHandler>();

        builder.Services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options => {
                options.SlidingExpiration = true;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(40);
                options.AccessDeniedPath = "/Forbidden";
            });

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment()) {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        // app.UseHttpsRedirection();

        app.UseRouting();

        app.UseAuthorization();
        app.UseAuthentication();

        app.MapStaticAssets();
        app.MapRazorPages()
            .WithStaticAssets();
        app.MapDefaultControllerRoute();

        app.Run();
    }
}
