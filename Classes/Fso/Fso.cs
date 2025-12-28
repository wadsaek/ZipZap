using System;
using System.Collections.Generic;

namespace ZipZap.Classes;


public abstract record Fso(FsoId Id, FsData Data) : IEntity<FsoId>, IFormattable {
    public override string ToString() => $"{Data.Permissions} {Data.FsoOwner}:{Data.FsoGroup} {Data.Name}";

    public string ToString(string? format)
        => format switch {
            LongListingFormat => ToString(),
            ShortListingFormat or _ => Data.Name
        };

    public string ToString(string? format, IFormatProvider? formatProvider) 
        => ToString(format);

    public const string LongListingFormat = "L";
    public const string ShortListingFormat = "S";
}
public sealed record File(FsoId Id, FsData Data) : Fso(Id, Data) {
    public byte[]? Content {get; init;}
    public override string ToString() => $"-{base.ToString()}";
}
public sealed record Symlink(FsoId Id, FsData Data, string Target) : Fso(Id, Data) {
    public override string ToString() => $"l{base.ToString()} -> {Target}";

}
public sealed record Directory(FsoId Id, FsData Data) : Fso(Id, Data) {
    public IEnumerable<Fso> MaybeChildren { get; init; } = [];
    public override string ToString() => $"d{base.ToString()}";
}

public record struct FsoId(Guid Value) : IStrongId {
    public readonly override string ToString() => Value.ToString();
}
