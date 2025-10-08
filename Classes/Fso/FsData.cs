using ZipZap.Classes.Helpers;

namespace ZipZap.Classes;
public class FsData {
    internal FsData() {
        VirtualLocation = null!;
        FsoOwner = null!;
        FsoGroup = null!;
    }
    public FsData(Option<Directory> virtualLocation, string fsoOwner, string fsoGroup) {
        VirtualLocation = virtualLocation;
        FsoOwner = fsoOwner;
        FsoGroup = fsoGroup;
    }

    public Option<Directory> VirtualLocation { get; set; }

    public string FsoOwner { get; set; }
    public string FsoGroup { get; set; }
}
