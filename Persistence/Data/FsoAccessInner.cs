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
