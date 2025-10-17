using System;

using ZipZap.Classes.Helpers;

namespace ZipZap.Classes;

public class User : IEntity<UserId> {
    public User(
        UserId id,
        string username,
        byte[] passwordHash,
        string email,
        MaybeEntity<Directory, FsoId> root
        ) {
        Id = id;
        Username = username;
        PasswordHash = passwordHash;
        Email = email;
        Root = root;
    }

    public UserId Id { get; set; }
    public string Username { get; set; }
    public byte[] PasswordHash { get; set; }
    public string Email { get; set; }
    public MaybeEntity<Directory, FsoId> Root { get; set; }
}

public record struct UserId(Guid Id);
