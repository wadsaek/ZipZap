using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.Logging;
using ICSharpCode.SharpZipLib.GZip;
using ZipZap.FileService.Extensions;
using System.Collections.Generic;

namespace ZipZap.FileService.Helpers;

using static Assertions;

public class IO : InterfaceIO
{
    private readonly IConfiguration _config;
    private readonly ILogger<IO> _logger;
    public IO(IConfiguration config, ILogger<IO> logger)
    {
        _config = config;
        _logger = logger;

    }

    private static readonly char[] _invalidCharacters = new char[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*', '.', '\0' };

    public bool IsValidPathChar(char c) =>
        c > 32 // control characters and ASCII space
        && c < 127 //extended ascii characters and ASCII delete
        && !_invalidCharacters.Contains(c);

    public bool IsValidPath(string path) => path.All(IsValidPathChar);

    public Task<bool> PathExistsAsync(string fileName)
        => Task.FromResult(File.Exists(GetFullPath(fileName)));

    private void AssertPath(string fileName)
        => Assert(IsValidPath(fileName), "Invalid fileName");

    private string GetFullPath(string fileName)
        => Path.Combine(_config.BaseFilePath, fileName);

    public async Task<byte[]> ReadAsync(string fileName)
    {
        AssertPath(fileName);

        fileName = GetFullPath(fileName);

        using Stream stream = File.OpenRead(fileName);
        using var decompressed = new GZipInputStream(stream);
        var contents = await decompressed.ToByteArrayAsync();
        return contents;
    }

    public async Task WriteAsync(string fileName, Stream content)
    {
        AssertPath(fileName);
        var fullPath = GetFullPath(fileName);

        using Mutex mut = new Mutex(false, fileName);
        _logger.LogInformation($"Created named mutex for the file {fullPath}");


        await RemoveAsync(fileName);
        mut.WaitOne();

        using FileStream file = File.OpenWrite(fullPath);
        GZip.Compress(content, file, true);

        mut.ReleaseMutex();
    }

    public Task RemoveAsync(string fileName)
    {
        AssertPath(fileName);

        var fullPath = GetFullPath(fileName);

        using Mutex mut = new Mutex(false, fileName);
        mut.WaitOne();
        File.Delete(fullPath);
        mut.ReleaseMutex();

        return Task.CompletedTask;
    }

    public async Task RemoveRangeAsync(IEnumerable<string> filenames)
    => await Task.WhenAll(
            filenames.Select(
                f => Task.Run(async () => await RemoveAsync(f))));

    public Task CopyAsync(string oldPath, string newPath)
    {
        AssertPath(oldPath); AssertPath(newPath);
        var (fullOldPath, fullNewPath) = (GetFullPath(oldPath), GetFullPath(newPath));

        using Mutex mut = new Mutex(false, fullNewPath);
        mut.WaitOne();
        File.Copy(fullOldPath, fullNewPath);
        mut.ReleaseMutex();

        return Task.CompletedTask;

    }
}
