using System.IO;
using System.Threading.Tasks;

namespace ZipZap.FileService.Extensions;

public static class StreamExt {
    public static async Task<byte[]> ToByteArrayAsync(this Stream stream) {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        return ms.ToArray();
    }
}
