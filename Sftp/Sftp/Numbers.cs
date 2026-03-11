// Numbers.cs - Part of the ZipZap project for storing files online
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

namespace ZipZap.Sftp.Sftp.Numbers;

using static Message;

public enum Message {
    Init = 1,
    Version = 2,
    Open = 3,
    Close = 4,
    Read = 5,
    Write = 6,
    Lstat = 7,
    Fstat = 8,
    SetStat = 9,
    FsetStat = 10,
    OpenDir = 11,
    ReadDir = 12,
    Remove = 13,
    Mkdir = 14,
    Rmdir = 15,
    Realpath = 16,
    Stat = 17,
    Rename = 18,
    ReadLink = 19,
    Symlink = 20,
    Status = 101,
    Handle = 102,
    Data = 103,
    Name = 104,
    Attrs = 105,
    Extended = 200,
    ExtendedReply = 201,
}
public static class MessageExt {
    extension(Message message) {
        public bool IsInitMessage() => message is Init or Version;
        public bool IsServerSideMessage() => (int)message switch {
            2 => true,
            > 100 and < 200 => true,
            201 => true,
            _ => false
        };
    }
}
public enum SftpError : uint {
    Ok = 0,
    Eof = 1,
    NoSuchFile = 2,
    PermissionDenied = 3,
    Failure = 4,
    BadMessage = 5,
    NoConnection = 6,
    ConnectionLost = 7,
    OpUnsupported = 8,
}

[Flags]
public enum OpenFlags : uint {

    Read = 0x01,
    Write = 0x02,
    Append = 0x04,
    Creat = 0x08,
    Trunc = 0x10,
    Excl = 0x20,
}
