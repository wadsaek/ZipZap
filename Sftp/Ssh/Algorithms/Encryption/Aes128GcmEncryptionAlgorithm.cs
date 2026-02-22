// Aes128GcmEncryptionAlgorithm.cs - Part of the ZipZap project for storing files online
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

using System.IO;

namespace ZipZap.Sftp.Ssh.Algorithms;

internal class Aes128GcmEncryptionAlgorithm : IEncryptionAlgorithm {
    public NameList.Item Name => new NameList.LocalName("aes128-gcm", "openssh.com");

    public bool OverridesMac => true;

    public int IVLength => 12;

    public int KeyLength => 16;

    public IDecryptor GetDecryptor(Stream stream, byte[] IV, byte[] Key, IMacValidator mac) {
        throw new System.NotImplementedException();
    }

    public IEncryptor GetEncryptor(byte[] IV, byte[] Key, IMacGenerator mac) {
        throw new System.NotImplementedException();
    }
}
