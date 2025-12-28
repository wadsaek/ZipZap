using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using ZipZap.Classes.Helpers;
using ZipZap.FileService.Extensions;

namespace ZipZap.FileService.Helpers;

using static Assertions;

public class IO : IIO {
    private readonly IConfiguration _config;
    private readonly ILogger<IO> _logger;
    public IO(IConfiguration config, ILogger<IO> logger) {
        _config = config;
        _logger = logger;

    }

    private static readonly char[] _invalidCharacters = ['<', '>', ':', '"', '/', '\\', '|', '?', '*', '.', '\0'];

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

    public async Task<Stream> ReadAsync(string fileName) {
        AssertPath(fileName);

        fileName = GetFullPath(fileName);
        var output = new MemoryStream();

        await using var stream = File.OpenRead(fileName);
        await using var decompressor = new GZipStream(stream, CompressionMode.Decompress);
        await decompressor.CopyToAsync(output);
        output.Position = 0;

        return output;
    }

    public async Task WriteAsync(string fileName, Stream content) {
        AssertPath(fileName);
        var fullPath = GetFullPath(fileName);

        using Mutex mut = new(false, fileName);
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Created named mutex for the file {fullPath}", fullPath);


        await RemoveAsync(fileName);
        mut.WaitOne();

        await using var file = File.OpenWrite(fullPath);
        await using var gzipStream = new GZipStream(file, CompressionMode.Compress);
        content.CopyTo(gzipStream);

        mut.ReleaseMutex();
    }

    public Task RemoveAsync(string fileName) {
        AssertPath(fileName);

        var fullPath = GetFullPath(fileName);

        using Mutex mut = new(false, fileName);
        mut.WaitOne();
        File.Delete(fullPath);
        mut.ReleaseMutex();

        return Task.CompletedTask;
    }

    public async Task RemoveRangeAsync(IEnumerable<string> filenames)
    => await Task.WhenAll(
            filenames.Select(
                f => Task.Run(async () => await RemoveAsync(f))));

    public Task CopyAsync(string oldPath, string newPath) {
        AssertPath(oldPath); AssertPath(newPath);
        var (fullOldPath, fullNewPath) = (GetFullPath(oldPath), GetFullPath(newPath));

        using Mutex mut = new(false, fullNewPath);
        mut.WaitOne();
        File.Copy(fullOldPath, fullNewPath);
        mut.ReleaseMutex();

        return Task.CompletedTask;

    }
}
