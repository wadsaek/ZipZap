using ZipZap.Classes.Helpers;

namespace ZipZap.Classes;

public class FsData {
    public FsData(Option<Directory> virtualLocation, string name, int fsoOwner, int fsoGroup, Permissions permissions) {
        VirtualLocation = virtualLocation;
        FsoOwner = fsoOwner;
        FsoGroup = fsoGroup;
        Permissions = permissions;
        Name = name;
    }

    public Option<Directory> VirtualLocation { get; set; }
    public Permissions Permissions { get; set; }

    public string Name { get; set; }
    public int FsoOwner { get; set; }
    public int FsoGroup { get; set; }
}
