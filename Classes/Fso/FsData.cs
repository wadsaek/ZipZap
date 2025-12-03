using ZipZap.Classes.Helpers;

namespace ZipZap.Classes;

public record FsData(
    MaybeEntity<Directory, FsoId>? VirtualLocation,
    Permissions Permissions,
    string Name,
    int FsoOwner,
    int FsoGroup
);
