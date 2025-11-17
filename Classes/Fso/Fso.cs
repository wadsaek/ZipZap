using System;
using System.Collections.Generic;

using ZipZap.Classes.Helpers;

using static ZipZap.Classes.Helpers.Constructors;

namespace ZipZap.Classes;


public abstract record Fso(FsoId Id, FsData Data) : IEntity<FsoId>;
public sealed record File(FsoId Id, FsData Data) : Fso(Id, Data) {
    public Option<byte[]> Content = None<byte[]>();
}
public sealed record Symlink(FsoId Id, FsData Data, string Target) : Fso(Id, Data);
public sealed record Directory(FsoId Id, FsData Data) : Fso(Id, Data) {
    // there is a semantic difference between an empty Directory and a directory, whose entries are unknown
    public Option<IEnumerable<Fso>> MaybeChildren { get; init; } = None<IEnumerable<Fso>>();
}

public record struct FsoId(Guid Value) : IStrongId {
    public override readonly string ToString() => Value.ToString();
}
