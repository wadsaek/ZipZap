// UserSshKey.cs - Part of the ZipZap project for storing files online
//     Copyright (C) 2026  Barenboim Esther wadsaek@gmail.com
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY, without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
//
//     You should have received a copy of the GNU General Public License
//     along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System;

using ZipZap.Classes.Helpers;

namespace ZipZap.Classes;

public sealed record UserSshKeyRaw(SshUserKeyId Id, SshPublicKey Key);

public sealed record UserSshKey(UserSshKeyRaw Raw, MaybeEntity<User, UserId> User)
: IEntity<SshUserKeyId> {
    public SshUserKeyId Id => Raw.Id;
    public SshPublicKey Key => Raw.Key;

    public UserSshKey(SshUserKeyId id, SshPublicKey key, MaybeEntity<User, UserId> user)
        : this(new(id, key), user) { }

    public static implicit operator UserSshKeyRaw(UserSshKey key) => key.Raw;
}
public static class UserSshKeyExt {
    extension(UserSshKeyRaw raw) {
        public UserSshKey Wrap(MaybeEntity<User, UserId> user)
            => new(raw, user);
    }
}

public record struct SshUserKeyId(Guid Id) : IStrongId;
