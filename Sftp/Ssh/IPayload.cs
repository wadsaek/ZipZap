// IPayload.cs - Part of the ZipZap project for storing files online
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

using System.Diagnostics.CodeAnalysis;

namespace ZipZap.Sftp.Ssh;

public interface IPayload {
    static abstract Numbers.Message Message { get; }
}
public interface IServerPayload : IPayload {
    public byte[] ToPayload();
}
public interface IClientPayload<T> : IPayload
where T : IClientPayload<T> {
    public abstract static bool TryParse(byte[] payload, [NotNullWhen(true)] out T? value);
}
public static class PayloadExt {
    extension(IServerPayload payload) {
        public Packet ToPacket(uint alignment, int offset = 0) {
            var bytes = payload.ToPayload();
            return new(bytes, alignment, offset);
        }
    }
}
