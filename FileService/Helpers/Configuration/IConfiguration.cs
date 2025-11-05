using ZipZap.Classes;

namespace ZipZap.FileService.Helpers;

public interface IConfiguration {
    public string BaseFilePath { get; init; }
    public FileSize MaximumFileSize { get; init; }
}
