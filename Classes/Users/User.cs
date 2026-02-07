using System;

using ZipZap.Classes.Helpers;

namespace ZipZap.Classes;

public record User(
         UserId Id,
         string Username,
         byte[] PasswordHash,
         string Email,
         UserRole Role,
         MaybeEntity<Directory, FsoId> Root
) : IEntity<UserId>;

public enum UserRole {
    User,
    Admin
}

public record struct UserId(Guid Value) : IStrongId {
    public readonly override string ToString() => Value.ToString();
}
