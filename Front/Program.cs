// Program.cs - Part of the ZipZap project for storing files online
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
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using ZipZap.Classes;
using ZipZap.Front.Factories;
using ZipZap.Front.Handlers.Exceptions;
using ZipZap.Front.Handlers.Files.View;
using ZipZap.Front.Services;
using ZipZap.LangExt.Helpers;
using ZipZap.Sftp;
using ZipZap.Sftp.Ssh.Algorithms;

using LoginError = ZipZap.Sftp.LoginError;

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
        builder.Services.AddSftp<SftpHandlerFactory>(new SftpConfiguration());

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
internal class SftpConfiguration : ISftpConfiguration {
    public int Port => 9999;
    public string ServerName => "ZipZapTestSftp";
    public string Version => "0.1.0";
    public RSA RsaKey { get; }
    public SftpConfiguration() {
        var pem = System.IO.File.ReadAllText("/home/wadsaek/Developing/ZipZap/Front/rsa/host");
        var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        RsaKey = rsa;
    }
}
internal class SftpHandlerFactory : ISftpRequestHandlerFactory {
    private readonly IFactory<IBackend, BackendConfiguration> _factory;
    private readonly ILoginService _login;

    public SftpHandlerFactory(IFactory<IBackend, BackendConfiguration> factory, ILoginService login) {
        _factory = factory;
        _login = login;
    }

    public ISftpLoginHandler CreateLogin() {
        return new SftpHandler(_factory, _login);
    }
}
internal class SftpHandler : ISftpLoginHandler, ISftpRequestHandler {
    private readonly IFactory<IBackend, BackendConfiguration> _backendFactory;
    private readonly ILoginService _login;

    private IBackend? _backend = null;

    public SftpHandler(IFactory<IBackend, BackendConfiguration> backendFactory, ILoginService login) {
        _backendFactory = backendFactory;
        _login = login;
    }

    public Task<Result<ISftpRequestHandler, LoginError>> TryLoginPublicKey(string username, IPublicKey userPublicKey, IHostKeyPair serverHostKey, CancellationToken cancellationToken) {
        return TryLoginPublicKeyRaw(3, username, userPublicKey, serverHostKey, cancellationToken);
    }
    private async Task<Result<ISftpRequestHandler, LoginError>> TryLoginPublicKeyRaw(uint triesLeft, string username, IPublicKey userPublicKey, IHostKeyPair serverHostKey, CancellationToken cancellationToken) {
        if (triesLeft == 0) return new Err<ISftpRequestHandler, LoginError>(new LoginError.Other());
        var result = await _login.LoginSsh(username, userPublicKey, serverHostKey, cancellationToken);
        return await result
        .Select(token => {
            _backend = _backendFactory.Create(new(token));
            return this as ISftpRequestHandler;
        })
        .ErrSelectManyAsync(async error => {
            if (error is SshLoginError.TimestampTooEarly or SshLoginError.TimestampWasUsed)
                return await TryLoginPublicKeyRaw(triesLeft - 1, username, userPublicKey, serverHostKey, cancellationToken);
            LoginError returned = error switch {
                SshLoginError.EmptyUsername => new LoginError.EmptyCredentials(),
                SshLoginError.UserPublicKeyDoesntMatch => new LoginError.WrongCredentials(),
                SshLoginError.HostPublicKeyNotAuthorized => new LoginError.HostPublicKeyNotAuthorized(),
                _ or SshLoginError.Other => new LoginError.Other()
            };
            return new Err<ISftpRequestHandler, LoginError>(returned);
        });
    }

    public async Task<Result<ISftpRequestHandler, LoginError>> TryLoginPassword(string username, string password, CancellationToken cancellationToken) {
        var result = await _login.Login(username, password, cancellationToken);
        return result
        .Select(token => {
            _backend = _backendFactory.Create(new(token));
            return this as ISftpRequestHandler;
        })
        .SelectErr(error => error switch {
            Services.LoginError.EmptyCredentials => new LoginError.EmptyCredentials() as LoginError,
            Services.LoginError.WrongCredentials => new LoginError.WrongCredentials(),
            _ => new LoginError.Other()
        });
    }
}
