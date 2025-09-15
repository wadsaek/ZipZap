namespace ZipZap.Classes;

public sealed class FsoAccess {
    public FsoAccess(Fso fso, User user) {
        Fso = fso;
        User = user;
    }

    public Fso Fso { get; set; }
    public User User { get; set; }
}
