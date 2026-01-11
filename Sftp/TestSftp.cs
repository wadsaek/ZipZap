using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using ZipZap.Sftp;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddConsole();
builder.Services.AddSingleton<ISftpRequestHandler, SftpHandler>();
builder.Services.AddSingleton<SftpService.ISftpConfiguration, SftpConfiguration>();

builder.Services.AddHostedService<SftpService>();
var app = builder.Build();
app.Run();

class SftpHandler : ISftpRequestHandler;
class SftpConfiguration : SftpService.ISftpConfiguration {
    public int Port => 9999;
    public string ServerName => "ZipZapTestSftp";
    public string Version => "0.1.0";
}
