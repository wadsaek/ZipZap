using System.Threading;
using System.Threading.Tasks;

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
        public async Task<PacketWithoutMac> ToPacket(CancellationToken cancellationToken) {
            var bytes = await payload.ToPayload(cancellationToken);
            return new(bytes);
        }
    }
}
