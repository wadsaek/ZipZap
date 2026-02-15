// UserInner.cs - Part of the ZipZap project for storing files online
//     Copyright (C) 2026  Barenboim Esther wadsaek@gmail.com
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY

using System;
using System.Security.Cryptography;

using ZipZap.Classes;
using ZipZap.Classes.Extensions;
using ZipZap.LangExt.Helpers;
using ZipZap.Persistence.Attributes;

namespace ZipZap.Persistence.Data;

[SqlTable("users")]
public class UserInner : ITranslatable<User>, ISqlRetrievable, IInner<Guid> {
    public UserInner(
        Guid id, string username, byte[] passwordHash, string email, Guid root,
        int uid, int gid,
        UserRole role
    ) {
        Id = id;
        Username = username;
        PasswordHash = passwordHash;
        Email = email;
        Role = role;
        Root = root;
        Uid = uid;
        Gid = gid;
    }

    public UserInner(UserInner other) : this(
            other.Id,
            other.Username,
            other.PasswordHash,
            other.Email,
            other.Root,
            other.Uid,
            other.Gid,
            other.Role
) { }

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

    [SqlColumn("role")]
    public UserRole Role { get; init; }

    [SqlColumn("uid")]
    public int Uid { get; init; }

    [SqlColumn("gid")]
    public int Gid { get; init; }

    public User Into() {
        PasswordHash.Length.AssertEq(SHA512.HashSizeInBytes);
        return new(
            new(Id), Username, PasswordHash, Email, Role,
            new(Uid, Gid),
            Root.ToFsoId().AsIdOf<Directory>()
        );
    }
    public static UserInner From(User user) {
        user.PasswordHash.Length.AssertEq(SHA512.HashSizeInBytes);

        return new(
            user.Id.Value,
            user.Username,
            user.PasswordHash,
            user.Email,
            user.Root.Id.Value,
            user.DefaultOwnership.FsoOwner,
            user.DefaultOwnership.FsoGroup,
            user.Role
        );
    }

    public UserInner Copy() => new(this);

    static ITranslatable<User> ITranslatable<User>.From(User entity)
        => From(entity);
}
