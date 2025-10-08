using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace ZipZap.FileService.Helpers;

public interface InterfaceIO {
    public Task WriteAsync(string path, Stream content);
    public Task<byte[]> ReadAsync(string path);
    public Task RemoveAsync(string path);
    public Task RemoveRangeAsync(IEnumerable<string> paths);
    public Task CopyAsync(string oldPath, string newPath);
    public bool IsValidPathChar(char c);
    public bool IsValidPath(string path);
    public Task<bool> PathExistsAsync(string path);
}
