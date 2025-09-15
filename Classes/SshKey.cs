namespace ZipZap.Classes;

public sealed class SshKey {
    public SshKey(string key, User user) {
        Key = key;
        User = user;
    }

    public string Key { get; set; }
    public User User { get; set; }
}
