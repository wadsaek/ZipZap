// Fso.cs - Part of the ZipZap project for storing files online
//     Copyright (C) 2026  Barenboim Esther wadsaek@gmail.com
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY, without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
//
//     You should have received a copy of the GNU General Public License
//     along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;

using ZipZap.LangExt.Helpers;

namespace ZipZap.Classes;


public abstract record Fso(FsoId Id, FsData Data) : IEntity<FsoId>, IFormattable {
    public override string ToString() => $"{Data.Permissions} {Data.Ownership} {Data.Name}";

    public string ToString(string? format)
        => format switch {
            LongListingFormat => ToString(),
            ShortListingFormat or _ => ToShortFormatString()
        };
    protected virtual string ToShortFormatString() => Data.Name;

    public string ToString(string? format, IFormatProvider? formatProvider)
        => ToString(format);

    public const string LongListingFormat = "L";
    public const string ShortListingFormat = "S";
}
public sealed record File(FsoId Id, FsData Data) : Fso(Id, Data) {
    public byte[]? Content { get; init; }
    public override string ToString() => $"-{base.ToString()}";
}
public sealed record Symlink(FsoId Id, FsData Data, string Target) : Fso(Id, Data) {
    public static Result<Symlink, SymlinkError> TryCreate(FsoId id, FsData data, string target) {
        if (string.IsNullOrWhiteSpace(target))
            return new Err<Symlink, SymlinkError>(new SymlinkError.EmptyTarget());

        return new Ok<Symlink, SymlinkError>(new(id, data, target));
    }

    public override string ToString() => $"l{base.ToString()} -> {Target}";
    protected override string ToShortFormatString() => $"{base.ToShortFormatString()} -> {Target}";

}

public record SymlinkError {
    public sealed record EmptyTarget : SymlinkError;
}

public sealed record Directory(FsoId Id, FsData Data) : Fso(Id, Data) {
    public IEnumerable<Fso> MaybeChildren { get; init; } = [];
    public override string ToString() => $"d{base.ToString()}";
}

public record struct FsoId(Guid Value) : IStrongId {
    public readonly override string ToString() => Value.ToString();
}
