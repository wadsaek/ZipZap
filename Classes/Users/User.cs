using System;

using ZipZap.Classes.Helpers;

namespace ZipZap.Classes;

public record User(
         UserId Id,
         string Username,
         byte[] PasswordHash,
         string Email,
         MaybeEntity<Directory, FsoId> Root
) : IEntity<UserId>;

public record struct UserId(Guid Value) {
    public override readonly string ToString() => Value.ToString();
}
