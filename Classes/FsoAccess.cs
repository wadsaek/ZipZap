using System;

using ZipZap.Classes.Helpers;

namespace ZipZap.Classes;

public sealed record FsoAccess(
    FsoAccessId Id,
    MaybeEntity<Fso, FsoId> Fso,
    MaybeEntity<User, UserId> User
) : IEntity<FsoAccessId>;

public record struct FsoAccessId(Guid Value) {
}
