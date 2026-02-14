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
