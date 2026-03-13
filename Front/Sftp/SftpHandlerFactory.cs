// SftpHandlerFactory.cs - Part of the ZipZap project for storing files online
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

using Microsoft.Extensions.Logging;

using ZipZap.Front.Factories;
using ZipZap.Front.Services;
using ZipZap.Sftp;

namespace ZipZap.Front.Sftp;

internal class SftpHandlerFactory : ISftpRequestHandlerFactory {
    private readonly IBackendFactory _factory;
    private readonly ILoginService _login;
    private readonly ILogger<SftpHandler> _logger;

    public SftpHandlerFactory(IBackendFactory factory, ILoginService login, ILogger<SftpHandler> logger) {
        _factory = factory;
        _login = login;
        _logger = logger;
    }

    public ISftpLoginHandler CreateLogin() {
        return new SftpLoginHandler(_factory, _login,_logger);
    }
}

