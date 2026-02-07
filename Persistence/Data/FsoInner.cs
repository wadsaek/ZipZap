using System;
using System.ComponentModel;

using ZipZap.Classes;
using ZipZap.Classes.Extensions;
using ZipZap.Persistence.Attributes;

namespace ZipZap.Persistence.Data;

[SqlTable("fsos")]
public class FsoInner : ITranslatable<Fso>, ISqlRetrievable, IInner<Guid> {
    public FsoInner(Guid id, string fsoName, Guid? virtualLocationId, short permissions, int fsoOwner, int fsoGroup, FsoType fsoType, string? linkRef) {
        Id = id;
        FsoName = fsoName;
        VirtualLocationId = virtualLocationId;
        Permissions = permissions;
        FsoOwner = fsoOwner;
        FsoGroup = fsoGroup;
        FsoType = fsoType;
        LinkRef = linkRef;
    }
    public FsoInner(FsoInner other) : this(
        other.Id,
        other.FsoName,
        other.VirtualLocationId,
        other.Permissions,
        other.FsoOwner,
        other.FsoGroup,
        other.FsoType,
        other.LinkRef
        ) { }
    public FsoInner Copy() => new(this);

    [SqlColumn("id")]
    public Guid Id { get; init; }

    [SqlColumn("fso_name")]
    public string FsoName { get; init; }

    [SqlColumn("virtual_location_id")]
    public Guid? VirtualLocationId { get; init; }

    [SqlColumn("permissions")]
    public short Permissions { get; init; }

    [SqlColumn("fso_owner")]
    public int FsoOwner { get; init; }

    [SqlColumn("fso_group")]
    public int FsoGroup { get; init; }

    [SqlColumn("fso_type")]
    public FsoType FsoType { get; init; }

    [SqlColumn("link_ref")]
    public string? LinkRef { get; init; }

    public Fso Into() {
        var virtualLocation = VirtualLocationId
        ?.ToFsoId()
        .AsIdOf<Directory>();

        var ownership = new Ownership(FsoOwner, FsoGroup);
        var data = new FsData(
        virtualLocation,
        Classes.Permissions.FromBitMask(Permissions),
        FsoName,
ownership
        );
        return FsoType switch {
            FsoType.RegularFile => new File(new FsoId(Id), data),
            FsoType.Directory => new Directory(new FsoId(Id), data),
            FsoType.Symlink => new Symlink(new FsoId(Id), data, LinkRef!),
            _ => throw new InvalidEnumArgumentException(nameof(FsoType))
        };
    }

    public static FsoInner From(Fso fso) => new(
        fso.Id.Value,
        fso.Data.Name,
        fso.Data.VirtualLocation?.Id.Value,
        (short)fso.Data.Permissions.Inner,
        fso.Data.Ownership.FsoOwner,
        fso.Data.Ownership.FsoGroup,
        fso switch {
            File => FsoType.RegularFile,
            Directory => FsoType.Directory,
            Symlink => FsoType.Symlink,
            _ => throw new InvalidEnumArgumentException(nameof(fso))
        },
        (fso as Symlink)?.Target
    );

    static ITranslatable<Fso> ITranslatable<Fso>.From(Fso entity) {
        return From(entity);
    }
}
