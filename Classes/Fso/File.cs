namespace ZipZap.Classes;

sealed public class File : Fso {
    public File(
        FsoId id,
        string name,
        FsData data,
        string dataPath,
        FilePermissions permissions
        ) : base(id, name, data) {
        DataPath = dataPath;
        Permissions = permissions;
    }

    public string DataPath { get; set; }
    public FilePermissions Permissions { get; set; }
}
