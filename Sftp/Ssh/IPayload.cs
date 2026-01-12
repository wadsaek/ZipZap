using System.Threading;
using System.Threading.Tasks;

using ZipZap.Sftp.Ssh.Algorithms;

namespace ZipZap.Sftp.Ssh;

internal interface IPayload {
    static abstract Numbers.Message Message { get; }
}
internal interface IServerPayload : IPayload {
    public Task<byte[]> ToPayload(CancellationToken cancellationToken);
}
internal interface IClientPayload<T> : IPayload
where T : IClientPayload<T> {
    public abstract static Task<T?> TryFromPayload(byte[] payload, CancellationToken cancellationToken);
}
static class PayloadExt {
    extension(IServerPayload payload) {
        public async Task<Packet> ToPacket(IMacAlgorithm macAlgorithm,CancellationToken cancellationToken) {
            var bytes = await payload.ToPayload(cancellationToken);
            var mac = await macAlgorithm.GenerateMacFor(bytes, cancellationToken);
            return new(bytes,mac);
        }
    }
}
