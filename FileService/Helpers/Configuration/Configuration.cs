using ZipZap.Classes;

namespace ZipZap.FileService.Helpers;

public class Configuration : IConfiguration {
    public string BaseFilePath { get; init; }
    public FileSize MaximumFileSize { get; init; }

    public Configuration(
        string baseFilePath,
        FileSize maximumFileSize) {
        BaseFilePath = baseFilePath;
        MaximumFileSize = maximumFileSize;
    }
}
