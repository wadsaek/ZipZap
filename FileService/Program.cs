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
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

using ZipZap.Classes;
using ZipZap.FileService.Helpers;
using ZipZap.FileService.Services;
using ZipZap.Persistence;

using Directory = System.IO.Directory;
using IConfiguration = ZipZap.FileService.Helpers.IConfiguration;

namespace ZipZap.FileService;

public class Program {
    private static string GetDataPath() {
        var basePath = Environment.CurrentDirectory;
        var envPath = Environment.GetEnvironmentVariable("FILE_SERVICE_FILES");
        if (envPath is not null)
            basePath = envPath;
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
            basePath = Environment.GetEnvironmentVariable("XDG_DATA_HOME") ?? $"{Environment.GetEnvironmentVariable("HOME")}/.local/share";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            basePath = Environment.GetEnvironmentVariable("HOME") + "/Library/";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            basePath = Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? throw new DirectoryNotFoundException("no app data on windows");
        var path = Path.Join(basePath, "FileService");
        if (!Path.Exists(path))
            Directory.CreateDirectory(path);
        return path;
    }
    public static RsaSecurityKey GetRsaSecurityKey() {

        var pem = System.IO.File.ReadAllText("/home/wadsaek/Developing/ZipZap/FileService/rsa/grpc");
        var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        var key = new RsaSecurityKey(rsa);
        return key;
    }
    public static void Main(string[] args) {
        var builder = WebApplication.CreateBuilder(args);

        const string connectionStringEnvVar = "NpgsqlConnectionString";
        var connectionString =
                builder.Configuration
                    .GetConnectionString(connectionStringEnvVar)
                ?? throw new(nameof(connectionStringEnvVar));
        builder.AddPersistence(connectionString);

        builder.Logging.AddConsole();
        var config = new Configuration(
                    GetDataPath(),
               FileSize.FromMegaBytes(16)
               );
        builder.Services.AddSingleton<IConfiguration>(config);

        builder.WebHost.ConfigureKestrel(serverOptions => {
            serverOptions.Limits.MaxRequestBufferSize = config.MaximumFileSize.AsBytes();
        });
        builder.Services.AddGrpc(options => {
            options.MaxReceiveMessageSize = (int)config.MaximumFileSize.AsBytes();
        });
        builder.Services.AddGrpcReflection();
        builder.Services.AddScoped<IIO, IO>();
        builder.Services.AddScoped<IUserService, UserService>();

        builder.Services.AddScoped(_ => GetRsaSecurityKey());

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options => {
                options.TokenValidationParameters = new() {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = GetRsaSecurityKey(),

                    ValidateLifetime = true
                };
            });
        builder.Services.AddAuthorization();


        var app = builder.Build();
        app.MapGrpcService<FilesStoringServiceImpl>();
        var env = app.Environment;
        if (env.EnvironmentName == "Development") {
            app.MapGrpcReflectionService();
        }

        // Configure the HTTP request pipeline.
        app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

        app.Run();
    }
}
