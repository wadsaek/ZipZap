// DI.cs - Part of the ZipZap project for storing files online
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

using Microsoft.Extensions.DependencyInjection;

using ZipZap.Sftp.Sftp;
using ZipZap.Sftp.Ssh.Algorithms;
using ZipZap.Sftp.Ssh.Services;
using ZipZap.Sftp.Ssh.Services.Connection;

namespace ZipZap.Sftp;

public static class DI {
    extension(IServiceCollection services) {
        public IServiceCollection AddSftp<T>(ISftpConfiguration configuration) where T : class, ISftpRequestHandlerFactory {
            services.AddScoped<SftpService>();
            services.AddScoped<Transport>();
            services.AddScoped<KeyExchangeProcess>();
            services.AddScoped<ISshChannelFactory, SshChannelFactory>();
            services.AddScoped<ISshConnectionFactory, SshConnectionFactory>();
            services.AddScoped<IAuthServiceFactory, AuthServiceFactory>();
            services.AddScoped<ISftpFactory, SftpFactory>();

            services.AddScoped<IEncryptionAlgorithm, Aes128GcmEncryptionAlgorithm >();
            services.AddScoped<Aes128GcmEncryptionAlgorithm>();

            services.AddScoped<IMacAlgorithm, HMacSha2256EtmOpenSsh>();
            services.AddScoped<HMacSha2256EtmOpenSsh>();

            services.AddScoped<IPublicKeyAlgorithm, RsaPublicKeyAlgorithm>();
            services.AddScoped<RsaPublicKeyAlgorithm>();
            services.AddScoped<RsaServerKeyAlgorithm>();

            services.AddScoped<IKeyExchangeAlgorithm, DiffieHelmanGroup14Sha256>();
            services.AddScoped<IServerHostKeyAlgorithm, RsaServerKeyAlgorithm>();
            services.AddScoped<ICompressionAlgorithm, NoneCompression>();

            services.AddScoped<ISftpRequestHandlerFactory, T>();

            services.AddSingleton(configuration);
            services.AddHostedService<SftpBackgroundService>();
            return services;
        }
    }
}
