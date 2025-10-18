using ZipZap.FileService.Extensions;

namespace ZipZap.FileService.Helpers;

public interface IConfiguration {
    public string BaseFilePath { get; init; }
    public FileSize MaximumFileSize { get; init; }
}
