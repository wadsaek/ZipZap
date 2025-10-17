using System;
using System.Collections.Generic;

using ZipZap.Classes.Helpers;

using static ZipZap.Classes.Helpers.Constructors;

namespace ZipZap.Classes;


public abstract record Fso(FsoId Id, FsData Data) : IEntity<FsoId>;
public sealed record File(FsoId Id, FsData Data) : Fso(Id, Data) {
    public string PhysicalPath => Id.Id.ToString();
}
public sealed record Symlink(FsoId Id, FsData Data, string Target) : Fso(Id, Data);
public sealed record Directory(FsoId Id, FsData Data) : Fso(Id, Data) {
    public Option<IEnumerable<Fso>> MaybeChildren { get; init; } = None<IEnumerable<Fso>>();
}

public record struct FsoId(Guid Id);
