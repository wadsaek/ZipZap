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

using ZipZap.Front.Factories;
using ZipZap.Front.Services;
using ZipZap.Sftp;

namespace ZipZap.Front.Sftp;

internal class SftpHandlerFactory : ISftpRequestHandlerFactory {
    private readonly IBackendFactory _factory;
    private readonly ILoginService _login;

    public SftpHandlerFactory(IBackendFactory factory, ILoginService login) {
        _factory = factory;
        _login = login;
    }

    public ISftpLoginHandler CreateLogin() {
        return new SftpHandler(_factory, _login);
    }
}

