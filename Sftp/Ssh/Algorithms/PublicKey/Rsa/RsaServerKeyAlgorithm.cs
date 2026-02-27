// RsaServerKeyAlgorithm.cs - Part of the ZipZap project for storing files online
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
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;

namespace ZipZap.Sftp.Ssh.Algorithms;

public class RsaServerKeyAlgorithm : IServerHostKeyAlgorithm {
    private readonly RsaPublicKeyAlgorithm _inner;
    private readonly ISftpConfiguration _sftpConfiguration;


    public RsaServerKeyAlgorithm(RsaPublicKeyAlgorithm inner, ISftpConfiguration sftpConfiguration) {
        _inner = inner;
        _sftpConfiguration = sftpConfiguration;
    }

    public NameList.Item Name => new NameList.GlobalName("rsa-sha2-256");

    public IEnumerable<NameList.Item> SupportedSignatureAlgs => _inner.SupportedSignatureAlgs;

    public bool TryParse(byte[] bytes, [NotNullWhen(true)] out IPublicKey? key) {
        return _inner.TryParse(bytes, out key);
    }

    class RsaPublicKeyPair : IHostKeyPair {
        private readonly RSA _rsaKey;
        private readonly HashAlgorithm _hashAlgorithm;
        const string ID = "rsa-sha2-256";

        public RsaPublicKeyPair(RSA rsaKey, HashAlgorithm hashAlgorithm) {

            _rsaKey = rsaKey;
            _hashAlgorithm = hashAlgorithm;
        }

        public IPublicKey GetPublicKey()
            => new RsaPublicKeyAlgorithm.RsaPublicKey(_rsaKey);

        public byte[] Sign(byte[] unsigned) {
            var hash = _hashAlgorithm.ComputeHash(unsigned);
            var formatter = new RSAPKCS1SignatureFormatter(_rsaKey);
            formatter.SetHashAlgorithm(nameof(SHA256));
            var signatureBlob = formatter.CreateSignature(hash);
            var sigAsInteger = new BigInteger(signatureBlob.Reverse().ToArray());
            return new SshMessageBuilder()
                .Write(ID)
                .WriteByteString(signatureBlob)
                .Build();
        }
    }


    public IHostKeyPair GetHostKeyPair() => new RsaPublicKeyPair(_sftpConfiguration.RsaKey ?? throw new System.Exception("no rsa key"), _inner.HashAlgorithm);
}

