using System;
using System.Security.Cryptography;

namespace ZipZap.Classes;

public class User : IEntity<UserId> {
    public User(
        UserId id,
        string username,
        SHA512 passwordHash,
        string email,
        Directory root
        ) {
        Id = id;
        Username = username;
        PasswordHash = passwordHash;
        Email = email;
        Root = root;
    }

    public UserId Id { get; set; }
    public string Username { get; set; }
    public SHA512 PasswordHash { get; set; }
    public string Email { get; set; }
    public Directory Root { get; set; }
}

public record struct UserId(Guid Id);
