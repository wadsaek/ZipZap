namespace ZipZap.Classes;

public sealed record FsoAccess(Fso Fso, User User) : IEntity<(FsoId, UserId)> {
    public (FsoId, UserId) Id => (Fso.Id, User.Id);
}
