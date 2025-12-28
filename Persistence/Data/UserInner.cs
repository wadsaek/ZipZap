
using System;
using System.Security.Cryptography;

using ZipZap.Classes;
using ZipZap.Classes.Extensions;
using ZipZap.Classes.Helpers;
using ZipZap.Persistence.Attributes;

namespace ZipZap.Persistence.Data;

[SqlTable("users")]
public class UserInner : ITranslatable<User>, ISqlRetrievable {
    public UserInner(Guid id, string username, byte[] passwordHash, string email, Guid root) {
        Id = id;
        Username = username;
        PasswordHash = passwordHash;
        Email = email;
        Root = root;
    }

    public UserInner(UserInner other) : this(
            other.Id,
            other.Username,
            other.PasswordHash,
            other.Email,
            other.Root) { }

    [SqlColumn("id")]
    public Guid Id { get; init; }

    [SqlColumn("username")]
    public string Username { get; init; }

    [SqlColumn("password_hash")]
    public byte[] PasswordHash { get; init; }

    [SqlColumn("email")]
    public string Email { get; init; }

    [SqlColumn("root")]
    public Guid Root { get; init; }

    public User Into() {
        PasswordHash.Length.AssertEq(SHA512.HashSizeInBytes);
        return new(new(Id), Username, PasswordHash, Email, Root.ToFsoId().AsIdOf<Directory>());
    }
    public static UserInner From(User user) {
        user.PasswordHash.Length.AssertEq(SHA512.HashSizeInBytes);

        return new(
            user.Id.Value,
            user.Username,
            user.PasswordHash,
            user.Email,
            user.Root.Id.Value
        );
    }

    public UserInner Copy() => new(this);

    static ITranslatable<User> ITranslatable<User>.From(User entity)
        => From(entity);
}
