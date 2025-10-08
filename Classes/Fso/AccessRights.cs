namespace ZipZap.Classes;

public struct AccessRights {
    public bool Read { get; set; }
    public bool Write { get; set; }
    public bool Execute { get; set; }
    public static AccessRights RW => new() { Read = true, Write = true };
    public static AccessRights RX => new() { Read = true, Execute = true };
    public static AccessRights RWX => new() { Read = true, Write = true, Execute = true };
    public static AccessRights ReadOnly => new() { Read = true };
}
public struct SetAccessRights {
    public bool Read { get; set; }
    public bool Write { get; set; }
    public ExecPermission Execute { get; set; }
    public static SetAccessRights RW => new() { Read = true, Write = true };
    public static SetAccessRights RX => new() { Read = true, Execute = ExecPermission.On };
    public static SetAccessRights RWX => new() { Read = true, Write = true, Execute = ExecPermission.On };
    public static SetAccessRights ReadOnly => new() { Read = true };
}

public enum ExecPermission {
    On,
    Off,
    Set
}

public static class ExecPermissionExt {
    extension(ExecPermission permission) {
        public static ExecPermission FromSetExec(bool set, bool exec) => exec
            ? set
                ? ExecPermission.Set
                : ExecPermission.On
            : ExecPermission.Off;
    }
}
