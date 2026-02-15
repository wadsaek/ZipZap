// DI.cs - Part of the ZipZap project for storing files online
//     Copyright (C) 2026  Barenboim Esther wadsaek@gmail.com
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY

using System.Collections.Immutable;

using Microsoft.Extensions.DependencyInjection;

using ZipZap.Sftp.Ssh;
using ZipZap.Sftp.Ssh.Algorithms;

namespace ZipZap.Sftp;

public static class DI {
    extension(IServiceCollection services) {
        public IServiceCollection AddSftp<T>(ISftpConfiguration configuration) where T : ISftpRequestHandler {
            services.AddScoped<IProvider<IEncryptionAlgorithm>, EncryptionAlgorithmProvider>();
            services.AddScoped<Aes128GcmEncryptionAlgorithm>();

            services.AddScoped<IProvider<IMacAlgorithm>, MacAlgorithmProvider>();
            services.AddScoped<HMacSha2256EtmOpenSsh>();

            services.AddScoped<RsaPublicKeyAlgorithm>();
            services.AddScoped<RsaServerKeyAlgorithm>();
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
    private readonly RsaServerKeyAlgorithm _rsaServerKeyAlgorithm;

    public ServerHostKeyProvider(RsaServerKeyAlgorithm rsaServerKeyAlgorithm) {
        _rsaServerKeyAlgorithm = rsaServerKeyAlgorithm;
    }

    public IImmutableList<IServerHostKeyAlgorithm> Items => [_rsaServerKeyAlgorithm];
}

internal class PublicKeyProvider : IProvider<IPublicKeyAlgorithm> {
    private readonly RsaPublicKeyAlgorithm _rsaPublicKeyAlgorithm;

    public PublicKeyProvider(RsaPublicKeyAlgorithm rsaPublicKeyAlgorithm) {
        _rsaPublicKeyAlgorithm = rsaPublicKeyAlgorithm;
    }

    public IImmutableList<IPublicKeyAlgorithm> Items => [_rsaPublicKeyAlgorithm];
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
