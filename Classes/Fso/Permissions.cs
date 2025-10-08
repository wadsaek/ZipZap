using System;
using System.Collections;

namespace ZipZap.Classes;

using static BitArrayIndexes;
public struct FilePermissions {
    public SetAccessRights OwnerAccessRights { get; set; }
    public SetAccessRights GroupAccessRights { get; set; }
    public AccessRights OtherAccessRights { get; set; }
    public static FilePermissions Default => new() {
        OwnerAccessRights = SetAccessRights.RW,
        GroupAccessRights = SetAccessRights.ReadOnly,
        OtherAccessRights = AccessRights.ReadOnly,
    };
    public static FilePermissions FromBitArray(BitArray array) {
        if (array.Length != 12) throw new ArgumentException($"{nameof(array)} has length other than 12");
        return new FilePermissions() {
            OwnerAccessRights = new SetAccessRights {
                Read = array[(int)OREAD],
                Write = array[(int)OWRITE],
                Execute = ExecPermission.FromSetExec(array[(int)SETUID], array[(int)OEXEC])
            },
            GroupAccessRights = new SetAccessRights {
                Read = array[(int)GREAD],
                Write = array[(int)GWRITE],
                Execute = ExecPermission.FromSetExec(array[(int)SETGID], array[(int)GEXEC])
            },
            OtherAccessRights = new AccessRights {
                Read = array[(int)OTREAD],
                Write = array[(int)OTWRITE],
                Execute = array[(int)OTEXEC]
            }
        };
    }
    public readonly BitArray ToBitArray() {
        BitArray array = new(12);
        array[(int)OREAD] = OwnerAccessRights.Read;
        array[(int)OWRITE] = OwnerAccessRights.Write;
        array[(int)OEXEC] = OwnerAccessRights.Execute != ExecPermission.Off;
        array[(int)GREAD] = GroupAccessRights.Read;
        array[(int)GWRITE] = GroupAccessRights.Write;
        array[(int)GEXEC] = GroupAccessRights.Execute != ExecPermission.Off;
        array[(int)OTREAD] = OtherAccessRights.Read;
        array[(int)OTWRITE] = OtherAccessRights.Write;
        array[(int)OTEXEC] = OtherAccessRights.Execute;
        array[(int)SETUID] = OwnerAccessRights.Execute == ExecPermission.Set;
        array[(int)SETGID] = GroupAccessRights.Execute == ExecPermission.Set;
        return array;
    }
}

public struct DirectoryPermissions {
    public AccessRights OwnerAccessRights { get; set; }
    public AccessRights GroupAccessRights { get; set; }
    public AccessRights OtherAccessRights { get; set; }
    public bool Sticky { get; set; }
    public static DirectoryPermissions Default => new() {
        OwnerAccessRights = AccessRights.RWX,
        GroupAccessRights = AccessRights.RX,
        OtherAccessRights = AccessRights.RX,
        Sticky = false,
    };
    public static DirectoryPermissions FromBitArray(BitArray array) {
        if (array.Length != 12) throw new ArgumentException($"{nameof(array)} has length other than 12");
        return new DirectoryPermissions() {
            OwnerAccessRights = new AccessRights {
                Read = array[(int)OREAD],
                Write = array[(int)OWRITE],
                Execute = array[(int)OEXEC]
            },
            GroupAccessRights = new AccessRights {
                Read = array[(int)GREAD],
                Write = array[(int)GWRITE],
                Execute = array[(int)GEXEC]
            },
            OtherAccessRights = new AccessRights {
                Read = array[(int)OTREAD],
                Write = array[(int)OTWRITE],
                Execute = array[(int)OTEXEC]
            },
            Sticky = array[(int)STICKY]
        };
    }
    public readonly BitArray ToBitArray() {
        BitArray array = new(12);
        array[(int)OREAD] = OwnerAccessRights.Read; array[(int)OWRITE] = OwnerAccessRights.Write;
        array[(int)OEXEC] = OwnerAccessRights.Execute;
        array[(int)GREAD] = GroupAccessRights.Read;
        array[(int)GWRITE] = GroupAccessRights.Write;
        array[(int)GEXEC] = GroupAccessRights.Execute;
        array[(int)OTREAD] = OtherAccessRights.Read;
        array[(int)OTWRITE] = OtherAccessRights.Write;
        array[(int)OTEXEC] = OtherAccessRights.Execute;
        array[(int)STICKY] = Sticky;
        return array;
    }
}

public enum BitArrayIndexes : int {
    STICKY = 0,
    SETUID = 1,
    SETGID = 2,
    OREAD = 3,
    OWRITE = 4,
    OEXEC = 5,
    GREAD = 6,
    GWRITE = 7,
    GEXEC = 8,
    OTREAD = 9,
    OTWRITE = 10,
    OTEXEC = 11
}
