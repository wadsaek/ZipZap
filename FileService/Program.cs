using ZipZap.FileService.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System;
using Microsoft.Extensions.Configuration;
using ZipZap.FileService.Helpers;
using ZipZap.FileService.Repositories;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Runtime.InteropServices;
using IConfiguration = ZipZap.FileService.Helpers.IConfiguration;
using Directory = System.IO.Directory;
using Microsoft.AspNetCore.Hosting;
using ZipZap.Classes;
using ZipZap.Classes.Helpers;
using ZipZap.FileService.Models;

namespace ZipZap.FileService;

public class Program {
    private static string GetDataPath() {
        string path = Environment.CurrentDirectory;
        var envPath = Environment.GetEnvironmentVariable("FILE_SERVICE_FILES");
        if (envPath is not null)
            path = envPath;
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
            path = Environment.GetEnvironmentVariable("XDG_DATA_HOME") + "/FileService";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            path = Environment.GetEnvironmentVariable("HOME") + "/Library/FileService";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            path = Environment.GetEnvironmentVariable("LOCALAPPDATA") + "\\FileService";
        if (!Path.Exists(path))
            Directory.CreateDirectory(path);
        return path;
    }
    public static void Main(string[] args) {
        var builder = WebApplication.CreateBuilder(args);

        var connectionStringEnvVar = "NpgsqlConnectionString";
        var connectionString =
                builder.Configuration
                    .GetConnectionString(connectionStringEnvVar)
                ?? throw new Exception(nameof(connectionStringEnvVar));
        builder.Services.AddNpgsqlDataSource(connectionString, builder => {
            builder.MapEnum<FsoType>();
        });

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
        builder.Services.AddScoped<InterfaceIO, IO>();
        builder.Services.AddScoped<ISecurityHelper, SecurityHelper>();
        builder.Services.AddSingleton<ExceptionConverter<DbError>>(new SimpleExceptionConverter<DbError>(err => new DbError()));
        builder.Services.AddScoped<IFsosRepository, FsosRepository>();

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
