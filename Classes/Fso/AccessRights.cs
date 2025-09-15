namespace ZipZap.Classes;
public struct AccessRights {
    public bool Read { get; set; }
    public bool Write { get; set; }
    public bool Execute { get; set; }
}
public struct SetAccessRights {
    public bool Read { get; set; }
    public bool Write { get; set; }
    public ExecPermission Execute { get; set; }
}

public enum ExecPermission {
    On,
    Off,
    Set
}

