namespace ZipZap.Classes;

public sealed record SshKey(string Key, User User) : IEntity<(string, UserId)> {
    public (string, UserId) Id => (Key, User.Id);
}
