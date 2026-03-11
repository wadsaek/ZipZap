// SftpConfiguration.cs - Part of the ZipZap project for storing files online
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

using System.Security.Cryptography;

using ZipZap.Sftp;

namespace ZipZap.Front.Sftp;

internal class SftpConfiguration : ISftpConfiguration {
    public int Port => 9999;
    public string ServerName => "ZipZapTestSftp";
    public string Version => "0.1.0";
    public RSA RsaKey { get; }
    public SftpConfiguration() {
        var pem = System.IO.File.ReadAllText("/home/wadsaek/Developing/ZipZap/Front/rsa/host");
        var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        RsaKey = rsa;
    }
}
