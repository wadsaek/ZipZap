using System;
using System.Collections.Generic;

using ZipZap.Classes.Helpers;

using static ZipZap.Classes.Helpers.Constructors;

namespace ZipZap.Classes;


public abstract record Fso(FsoId Id, FsData Data) : IEntity<FsoId>{
    public override string ToString() => $"{Data.Permissions} {Data.FsoOwner}:{Data.FsoOwner} {Data.Name}";
}
public sealed record File(FsoId Id, FsData Data) : Fso(Id, Data) {
    public Option<byte[]> Content = None<byte[]>();
    public override string ToString() => $"-{base.ToString()}";
}
public sealed record Symlink(FsoId Id, FsData Data, string Target) : Fso(Id, Data){
    public override string ToString() => $"l{base.ToString()} -> {Target}";
}
public sealed record Directory(FsoId Id, FsData Data) : Fso(Id, Data) {
    // there is a semantic difference between an empty Directory and a directory, whose entries are unknown
    public Option<IEnumerable<Fso>> MaybeChildren { get; init; } = None<IEnumerable<Fso>>();
    public override string ToString() => $"d{base.ToString()}";
}

public record struct FsoId(Guid Value) : IStrongId {
    public override readonly string ToString() => Value.ToString();
}
