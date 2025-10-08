namespace ZipZap.Classes;

public sealed class SshKey : IEntity<(string, UserId)> {
    public SshKey(string key, User user) {
        Key = key;
        User = user;
    }

    public string Key { get; set; }
    public User User { get; set; }

    public (string, UserId) Id => (Key, User.Id);
}
