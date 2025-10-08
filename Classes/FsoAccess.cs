namespace ZipZap.Classes;

public sealed class FsoAccess : IEntity<(FsoId, UserId)> {
    public FsoAccess(Fso fso, User user) {
        Fso = fso;
        User = user;
    }

    public Fso Fso { get; set; }
    public User User { get; set; }

    public (FsoId, UserId) Id => (Fso.Id, User.Id);
}
