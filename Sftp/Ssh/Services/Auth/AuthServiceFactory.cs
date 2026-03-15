// AuthServiceFactory.cs - Part of the ZipZap project for storing files online
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

using System.Collections.Generic;

using Microsoft.Extensions.Logging;

using ZipZap.Sftp.Ssh.Algorithms;
using ZipZap.Sftp.Ssh.Services.Connection;

namespace ZipZap.Sftp.Ssh.Services;

internal class AuthServiceFactory : IAuthServiceFactory {
    private readonly ISftpRequestHandlerFactory _factory;
    private readonly IEnumerable<IPublicKeyAlgorithm> _publicKeys;
    private readonly IEnumerable<IServerHostKeyAlgorithm> _hostKeys;
    private readonly ISshConnectionFactory _connectionFactory;
    private readonly ILogger<AuthService> _logger;

    public AuthServiceFactory(
        ISftpRequestHandlerFactory factory,
        IEnumerable<IPublicKeyAlgorithm> publicKeys,
        IEnumerable<IServerHostKeyAlgorithm> hostKeys,
        ISshConnectionFactory connectionFactory,
        ILogger<AuthService> logger
    ) {
        _factory = factory;
        _publicKeys = publicKeys;
        _hostKeys = hostKeys;
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public IAuthService Create(ITransportClient transport) {
        return new AuthService(
            transport,
            _factory.CreateLogin(),
            _publicKeys,
            _hostKeys,
            _connectionFactory,
            _logger
        );
    }
}

