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

using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

using ZipZap.Sftp.Ssh.Algorithms;

namespace ZipZap.Sftp.Ssh;

internal interface IPayload {
    static abstract Numbers.Message Message { get; }
}
internal interface IServerPayload : IPayload {
    public byte[] ToPayload();
}
internal interface IClientPayload<T> : IPayload
where T : IClientPayload<T> {
    public abstract static Task<T?> TryFromPayload(byte[] payload, CancellationToken cancellationToken);
}
static class PayloadExt {
    extension(IServerPayload payload) {
        public async Task<Packet> ToPacket(IMacAlgorithm macAlgorithm, uint sequencial, BigInteger secret, CancellationToken cancellationToken) {
            var bytes = payload.ToPayload();
            var mac = await macAlgorithm.GenerateMacForAsync(sequencial, secret, bytes, cancellationToken);
            return new(bytes, mac);
        }
        public async Task<Packet> ToPacket(SshState sshState, CancellationToken cancellationToken) => await payload.ToPacket(
            sshState.MacData.MacAlgorithm,
            sshState.MacData.MacSequenceClient,
            sshState.Secret,
            cancellationToken
        );
    }
}
