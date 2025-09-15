namespace ZipZap.Classes;

sealed public class Directory : Fso {

    public Directory(
        FsoId id,
        string name,
        FsData data,
        DirectoryPermissions permissions
        ) : base(id, name, data) {
        Permissions = permissions;
    }

    public DirectoryPermissions Permissions { get; set; }
}
