namespace ZipZap.Classes;

sealed public class File : Fso {

    public File() {
        Permissions = default;
        PhysicalPath = null!;
    }

    public File(
        FsoId id,
        string name,
        FsData data,
        string dataPath,
        FilePermissions permissions
        ) : base(id, name, data) {
        PhysicalPath = dataPath;
        Permissions = permissions;
    }

    public string PhysicalPath { get; set; }
    public FilePermissions Permissions { get; set; }
}
