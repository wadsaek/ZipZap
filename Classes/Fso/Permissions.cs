namespace ZipZap.Classes;

public struct FilePermissions {
    public SetAccessRights OwnerAccessRights { get; set; }
    public SetAccessRights GroupAccessRights { get; set; }
    public AccessRights OtherAccessRights { get; set; }
}

public struct DirectoryPermissions {
    public AccessRights OwnerAccessRights { get; set; }
    public AccessRights GroupAccessRights { get; set; }
    public AccessRights OtherAccessRights { get; set; }
}
