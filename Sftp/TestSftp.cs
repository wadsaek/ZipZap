using System.IO;
using System.Security.Cryptography;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using ZipZap.Sftp;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddConsole();
builder.Services.AddSingleton<ISftpRequestHandler, SftpHandler>();
builder.Services.AddSftp<SftpHandler>(new SftpConfiguration(
    builder.Configuration["RSA:PrivateKey"]
    ?? throw new System.Exception("No Private key"))
);
var app = builder.Build();
app.Run();

internal class SftpHandler : ISftpRequestHandler;
internal class SftpConfiguration : ISftpConfiguration {
    public int Port => 9999;
    public string ServerName => "ZipZapTestSftp";
    public string Version => "0.1.0";
    public RSA RsaKey { get; }
    public SftpConfiguration(string rsaPath) {
        var pem = File.ReadAllText(rsaPath);
        var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        RsaKey = rsa;
    }
}

