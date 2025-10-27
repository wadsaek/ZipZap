
using System;
using System.Security.Cryptography;

using ZipZap.Classes;
using ZipZap.Classes.Helpers;
using ZipZap.Persistance.Attributes;

using static ZipZap.Classes.Helpers.Constructors;

namespace ZipZap.Persistance.Data;

[SqlTable("users")]
public class UserInner : ITranslatable<User>, ISqlRetrievable {

    [SqlColumn("id")]
    public Guid Id { get; set; }

    [SqlColumn("username")]
    public required string Username { get; set; }

    [SqlColumn("password_hash")]
    public required byte[] PasswordHash { get; set; }

    [SqlColumn("email")]
    public required string Email { get; set; }

    [SqlColumn("root")]
    public required Guid Root { get; set; }

    public User Into() {
        Assertions.AssertEq(PasswordHash.Length, SHA512.HashSizeInBytes);
        return new(new(Id), Username, PasswordHash, Email, OnlyId<Directory, FsoId>(new FsoId(Root)));
    }
    public static UserInner From(User user) {
        Assertions.AssertEq(user.PasswordHash.Length, SHA512.HashSizeInBytes);

        return new() {
            Email = user.Email,
            PasswordHash = user.PasswordHash,
            Root = user.Root.Id.Value,
            Username = user.Username,
            Id = user.Id.Value
        };
    }

    public UserInner Copy() => From(Into());

    static ITranslatable<User> ITranslatable<User>.From(User entity)
        => From(entity);
}
