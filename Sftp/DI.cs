using System.Collections.Immutable;

using Microsoft.Extensions.DependencyInjection;

using ZipZap.Sftp.Ssh;
using ZipZap.Sftp.Ssh.Algorithms;

using static ZipZap.Sftp.SftpService;

namespace ZipZap.Sftp;

public static class DI {
    extension(IServiceCollection services) {
        public IServiceCollection AddSftp<T>(ISftpConfiguration configuration) where T : ISftpRequestHandler {
            services.AddScoped<IProvider<IEncryptionAlgorithm>, EncryptionAlgorithmProvider>();
            services.AddScoped<Aes128GcmEncryptionAlgorithm>();

            services.AddScoped<IProvider<IMacAlgorithm>, MacAlgorithmProvider>();
            services.AddScoped<HMacSha2256EtmOpenSsh>();

            services.AddScoped<IProvider<IKeyExchangeAlgorithm>, KeyExchangeAlgorithmProvider>();
            services.AddScoped<IProvider<IPublicKeyAlgorithm>, PublicKeyProvider>();
            services.AddScoped<IProvider<IServerHostKeyAlgorithm>, ServerHostKeyProvider>();
            services.AddScoped<IProvider<ICompressionAlgorithm>, CompressionProvider>();

            services.AddSingleton<ISftpRequestHandler, SftpHandler>();

            services.AddSingleton(configuration);
            services.AddHostedService<SftpService>();
            return services;
        }
    }
}

internal class ServerHostKeyProvider : IProvider<IServerHostKeyAlgorithm> {
    public IImmutableList<IServerHostKeyAlgorithm> Items => [new RsaKeyAlgorithm()];
}

internal class PublicKeyProvider : IProvider<IPublicKeyAlgorithm> {
    public IImmutableList<IPublicKeyAlgorithm> Items => [new RsaKeyAlgorithm()];
}
internal class CompressionProvider : IProvider<ICompressionAlgorithm> {
    public IImmutableList<ICompressionAlgorithm> Items => [new NoneCompression()];
}

internal class KeyExchangeAlgorithmProvider : IProvider<IKeyExchangeAlgorithm> {
    public IImmutableList<IKeyExchangeAlgorithm> Items => [new DiffieHelmanGroup14Sha256()];
}

internal class MacAlgorithmProvider : IProvider<IMacAlgorithm> {
    private readonly HMacSha2256EtmOpenSsh _hMacSha2256EtmOpenSsh;

    public MacAlgorithmProvider(HMacSha2256EtmOpenSsh hMacSha2256EtmOpenSsh) {
        _hMacSha2256EtmOpenSsh = hMacSha2256EtmOpenSsh;
    }

    public IImmutableList<IMacAlgorithm> Items => [_hMacSha2256EtmOpenSsh];
}

internal class EncryptionAlgorithmProvider : IProvider<IEncryptionAlgorithm> {
    private readonly Aes128GcmEncryptionAlgorithm _aesGcm;

    public EncryptionAlgorithmProvider(Aes128GcmEncryptionAlgorithm aesGcm) {
        _aesGcm = aesGcm;
    }

    public IImmutableList<IEncryptionAlgorithm> Items => [_aesGcm];
}

internal class Aes128GcmEncryptionAlgorithm : IEncryptionAlgorithm {
    public NameList.Item Name => new NameList.LocalName("aes128-gcm", "openssh.com");
}
