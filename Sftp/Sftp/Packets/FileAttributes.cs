// FileAttributes.cs - Part of the ZipZap project for storing files online
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
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;

using ZipZap.Sftp.Ssh;

namespace ZipZap.Sftp.Sftp;

public record FileAttributes(
    ulong? Size,
    Ownership? Ownership,
    UnixFileMode? Permissions,
    AcModTimes? Times,
    ImmutableList<AttrExtension> Extensions
    ) {

    internal byte[] ToByteArray() {
        var flags = FileAttributesFlags.None;
        if (Size is not null) flags |= FileAttributesFlags.Size;
        if (Ownership is not null) flags |= FileAttributesFlags.UidGid;
        if (Permissions is not null) flags |= FileAttributesFlags.Permissions;
        if (Times is not null) flags |= FileAttributesFlags.ACModtime;
        if (Extensions is not []) flags |= FileAttributesFlags.Extended;
        var builder = new SshMessageBuilder();
        builder.Write((uint)flags);
        if (Size is not null) builder.Write(Size.Value);
        if (Ownership is var (uid, gid)) builder.Write(uid).Write(gid);
        if (Permissions is not null) builder.Write((uint)Permissions);
        if (Times is var (access, modification))
            // NOTE: this is vulnerable to the year 2038 problem, but
            // that's what the protocol says to do...
            // we can support an extension here in order to send 
            // the untruncated timestamp but Openssh doesn't support it so it
            // wouldn't matter that much.
            // Also this is a bit outside the scope since `ZipZap.Front`
            // doesn't even have support for providing us with timestamp data
            builder
                .Write((uint)access.ToUnixTimeSeconds())
                .Write((uint)modification.ToUnixTimeSeconds());
        if (Extensions is not []) {
            builder.Write(Extensions.Count);
            foreach (var ext in Extensions)
                builder
                    .Write(ext.Name)
                    .WriteByteString(ext.Data);
        }
        return builder.Build();
    }
    public static FileAttributes Empty => new(null, null, null, null, []);
    internal static bool TryParse(Stream stream, [NotNullWhen(true)] out FileAttributes? attrs) {
        attrs = null;
        if (!stream.SshTryReadUint32Sync(out var flagsRaw)) return false;
        ulong? size = null;
        Ownership? ownership = null;
        UnixFileMode? perms = null;
        AcModTimes? times = null;
        List<AttrExtension> extensions = [];
        var flags = (FileAttributesFlags)flagsRaw;
        if (flags.HasFlag(FileAttributesFlags.Size)) {
            if (!stream.SshTryReadUInt64Sync(out var sizeraw)) return false;
            size = sizeraw;
        }
        if (flags.HasFlag(FileAttributesFlags.UidGid)) {
            if (!stream.SshTryReadUint32Sync(out var uid)) return false;
            if (!stream.SshTryReadUint32Sync(out var gid)) return false;
            ownership = new(uid, gid);
        }
        if (flags.HasFlag(FileAttributesFlags.Permissions)) {
            if (!stream.SshTryReadUint32Sync(out var permsRaw)) return false;
            perms = (UnixFileMode)permsRaw;
        }
        if (flags.HasFlag(FileAttributesFlags.ACModtime)) {
            if (!stream.SshTryReadUint32Sync(out var atime)) return false;
            if (!stream.SshTryReadUint32Sync(out var mtime)) return false;
            times = new(
                DateTimeOffset.FromUnixTimeSeconds(atime),
                DateTimeOffset.FromUnixTimeSeconds(mtime)
            );
        }
        if (flags.HasFlag(FileAttributesFlags.Extended)) {
            if (!stream.SshTryReadUint32Sync(out var count)) return false;
            for (var i = 0; i < count; i++) {
                if (!stream.SshTryReadStringSync(out var type)) return false;
                if (!stream.SshTryReadByteStringSync(out var data)) return false;
                extensions.Add(new(type, data));
            }
        }
        attrs = new(size, ownership, perms, times, extensions.ToImmutableList());
        return true;
    }
}

[Flags]
internal enum FileAttributesFlags : uint {
    None = 0,
    Size = 0X00000001,
    UidGid = 0x00000002,
    Permissions = 0x00000004,
    ACModtime = 0x00000008,
    Extended = 0x80000000,
}
public record AttrExtension(string Name, byte[] Data) {
}

public record Ownership(uint UserId, uint GroupId);
public record AcModTimes(DateTimeOffset AccessTime, DateTimeOffset ModificationTime);
