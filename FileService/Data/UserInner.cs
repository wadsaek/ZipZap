
using System;
using System.Collections;
using System.Security.Cryptography;

using ZipZap.Classes;
using ZipZap.Classes.Helpers;
using ZipZap.FileService.Attributes;

using static ZipZap.Classes.Helpers.Constructors;

namespace ZipZap.FileService.Data;

[SqlTable("users")]
public class UserInner : ITranslatable<User>, ISqlRetrievable {

    [SqlColumn("id")]
    public Guid Id { get; set; }

    [SqlColumn("username")]
    public required string Username { get; set; }

    [SqlColumn("password_hash")]
    public required BitArray PasswordHash { get; set; }

    [SqlColumn("email")]
    public required string Email { get; set; }

    [SqlColumn("root")]
    public required Guid Root { get; set; }

    public User Into() {
        Assertions.AssertEq(PasswordHash.Length, SHA512.HashSizeInBits);
        var passwordHashBytes = new byte[SHA512.HashSizeInBytes];
        PasswordHash.CopyTo(passwordHashBytes, 0);
        return new(new(Id), Username, passwordHashBytes, Email, OnlyId<Directory, FsoId>(new FsoId(Root)));
    }
    public static UserInner From(User user) {
        Assertions.AssertEq(user.PasswordHash.Length, SHA512.HashSizeInBytes);
        var bits = new BitArray(user.PasswordHash);

        return new() {
            Email = user.Email,
            PasswordHash = bits,
            Root = user.Root.Id.Value,
            Username = user.Username,
            Id = user.Id.Value
        };
    }

    public UserInner Copy() => From(Into());

    static ITranslatable<User> ITranslatable<User>.From(User entity)
        => From(entity);
}
