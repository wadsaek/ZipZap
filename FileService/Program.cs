using System;
using System.IO;
using System.Runtime.InteropServices;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using ZipZap.Classes;
using ZipZap.Classes.Helpers;
using ZipZap.FileService.Data;
using ZipZap.FileService.Extensions;
using ZipZap.FileService.Helpers;
using ZipZap.FileService.Models;
using ZipZap.FileService.Repositories;
using ZipZap.FileService.Services;

using Directory = System.IO.Directory;
using IConfiguration = ZipZap.FileService.Helpers.IConfiguration;

namespace ZipZap.FileService;

public class Program {
    private static string GetDataPath() {
        string basePath = Environment.CurrentDirectory;
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
        builder.Services.AddScoped<IIO, IO>();
        builder.Services.AddScoped<ISecurityHelper, SecurityHelper>();
        builder.Services.AddSingleton<ExceptionConverter<DbError>>(new SimpleExceptionConverter<DbError>(err => new DbError()));
        builder.Services.AddScoped(typeof(BasicRepository<,,>));
        builder.Services.AddScoped<IFsosRepository, FsosRepository>();
        builder.Services.AddScoped<EntityHelper<FsoInner, Fso, Guid>, FsoHelper>();


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
