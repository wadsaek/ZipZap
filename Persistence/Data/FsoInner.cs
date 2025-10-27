using System;
using System.Collections;
using System.ComponentModel;

using ZipZap.Classes;
using ZipZap.Classes.Extensions;
using ZipZap.Classes.Helpers;
using ZipZap.Persistance.Attributes;

namespace ZipZap.Persistance.Data;

[SqlTable("fsos")]
public class FsoInner : ITranslatable<Fso>, ISqlRetrievable {
    public FsoInner() {
    }
    public FsoInner Copy() => From(Into());

    [SqlColumn("id")]
    public required Guid Id { get; set; }

    [SqlColumn("fso_name")]
    public required string FsoName { get; set; }

    [SqlColumn("virtual_location_id")]
    public required Guid? VirtualLocationId { get; set; }

    [SqlColumn("permissions")]
    public required BitArray Permissions { get; set; }

    [SqlColumn("fso_owner")]
    public required int FsoOwner { get; set; }

    [SqlColumn("fso_group")]
    public required int FsoGroup { get; set; }

    [SqlColumn("fso_type")]
    public required FsoType FsoType { get; set; }

    [SqlColumn("link_ref")]
    public required string? LinkRef { get; set; }

    public Fso Into() {
        var virtualLocation = VirtualLocationId
        .ToOption()
        .Select(id => new FsoId(id))
        .Select(Directory.WithId);

        var data = new FsData(
        virtualLocation,
        Classes.Permissions.FromBitArray(Permissions),
        FsoName,
        FsoOwner,
        FsoGroup
        );
        return FsoType switch {
            FsoType.RegularFile => new File(new FsoId(Id), data),
            FsoType.Directory => new Directory(new FsoId(Id), data),
            FsoType.Symlink => new Symlink(new FsoId(Id), data, LinkRef!),
            _ => throw new InvalidEnumArgumentException(nameof(FsoType))
        };
    }

    public static FsoInner From(Fso fso) => new() {
        Id = fso.Id.Value,
        FsoGroup = fso.Data.FsoGroup,
        FsoName = fso.Data.Name,
        FsoOwner = fso.Data.FsoOwner,
        LinkRef = (fso as Symlink)?.Target,
        Permissions = fso.Data.Permissions.ToBitArray(),
        VirtualLocationId = fso.Data.VirtualLocation.Select(dir => dir.Id.Value),
        FsoType = fso switch {
            File => FsoType.RegularFile,
            Directory => FsoType.Directory,
            Symlink => FsoType.Symlink,
            _ => throw new InvalidEnumArgumentException(nameof(fso))
        }
    };

    static ITranslatable<Fso> ITranslatable<Fso>.From(Fso entity) {
        return From(entity);
    }
}
