using ZipZap.Classes.Helpers;

namespace ZipZap.Classes;

public record FsData(
    MaybeEntity<Directory, FsoId>? VirtualLocation,
    Permissions Permissions,
    string Name,
    Ownership Ownership
);

public record Ownership(
    int FsoOwner,
    int FsoGroup
){
public override string ToString() => $"{FsoOwner}:{FsoGroup}";
}
