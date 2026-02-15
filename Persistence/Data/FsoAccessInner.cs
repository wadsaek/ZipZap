// FsoAccessInner.cs - Part of the ZipZap project for storing files online
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

using ZipZap.Classes;
using ZipZap.Classes.Extensions;
using ZipZap.Persistence.Attributes;

namespace ZipZap.Persistence.Data;

[SqlTable("fso_access")]
public class FsoAccessInner : ITranslatable<FsoAccess>, ISqlRetrievable, IInner<Guid> {
    public FsoAccessInner(Guid id, Guid fsoId, Guid userId) {
        Id = id;
        FsoId = fsoId;
        UserId = userId;
    }
    public FsoAccessInner(FsoAccessInner other) : this(other.Id, other.FsoId, other.UserId) { }
    public FsoAccessInner Copy() => new(this);


    [SqlColumn("id")]
    public Guid Id { get; init; }

    [SqlColumn("user_id")]
    public Guid UserId { get; init; }

    [SqlColumn("fso_id")]
    public Guid FsoId { get; init; }

    public FsoAccess Into() => new(Id.ToFsoAccessId(), FsoId.ToFsoId(), UserId.ToUserId());

    public static FsoAccessInner From(FsoAccess fso) => new(
        fso.Id.Value,
        fso.User.Id.Value,
        fso.Fso.Id.Value
    );

    static ITranslatable<FsoAccess> ITranslatable<FsoAccess>.From(FsoAccess entity) {
        return From(entity);
    }
}
