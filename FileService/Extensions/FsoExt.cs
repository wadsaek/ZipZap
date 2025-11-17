using ZipZap.Classes;

namespace ZipZap.FileService.Extensions;

public static class FsoExt {
    extension(File file) {
        public string PhysicalPath => file.Id.ToString();
    }

}
