using ZipZap.Classes.Helpers;

namespace ZipZap.Classes;

public record FsData(
    Option<MaybeEntity<Directory, FsoId>> VirtualLocation,
    Permissions Permissions,
    string Name,
    int FsoOwner,
    int FsoGroup
);
