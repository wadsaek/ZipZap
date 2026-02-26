// RsaPublicKeyAlgorithm.cs - Part of the ZipZap project for storing files online
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

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;

using ZipZap.LangExt.Extensions;

namespace ZipZap.Sftp.Ssh.Algorithms;

public class RsaPublicKeyAlgorithm() : IPublicKeyAlgorithm {
    static NameList.Item StaticName => new NameList.GlobalName("ssh-rsa");
    public NameList.Item Name => StaticName;

    public class RsaPublicKey : IPublicKey {
        public RsaPublicKey(RSA rsa) {
            _rsa = rsa;
        }
        private readonly RSA _rsa;

        public string AlgorithmName => StaticName.ToString();

        public bool Verify(byte[] signature, byte[] data) {
            var stream = new MemoryStream(signature);
            if (!stream.SshTryReadStringSync(out var str)) return false;
            HashAlgorithmName? name = str switch {
                "rsa-sha2-256" => HashAlgorithmName.SHA256,
                "rsa-sha2-512" => HashAlgorithmName.SHA512,
                _ => null
            };
            if (name is null) return false;
            if (!stream.SshTryReadByteStringSync(out var blob))
                return false;
            return _rsa.VerifyData(data, blob, name.Value, RSASignaturePadding.Pkcs1);
        }

        public byte[] ToByteString() {
            // NOTE:
            // these are encoded as unsigned big-endian.
            // You can verify this through trial and error.
            // There is 0 reason Microsoft can't document it.
            // There is even less reason for this to be the EXACT OPPOSITE of
            // the default BigInteger constructor's parameters.
            var parameters = _rsa.ExportParameters(false);
            var modulus = new BigInteger(parameters.Modulus, isUnsigned: true, isBigEndian: true);
            var exponent = new BigInteger(parameters.Exponent, isUnsigned: true, isBigEndian: true);
            return new SshMessageBuilder()
                .Write(AlgorithmName)
                .Write(exponent)
                .Write(modulus)
                .Build();

        }

        public string ToAsciiString() {
            var bytes = ToByteString();
            var hex = Convert.ToHexStringLower(bytes);
            return new[] { AlgorithmName, hex }.ConcatenateWith(" ");
        }
    }

    public static bool TryParseRsa(byte[] bytes, [NotNullWhen(true)] out RSA? rsa) {

        var stream = new MemoryStream(bytes);
        rsa = null;
        if (!stream.SshTryReadStringSync(out var str)) return false;
        if (str != StaticName.ToString()) return false;
        if (!stream.SshTryReadBigIntSync(out var exponent)) return false;
        if (!stream.SshTryReadBigIntSync(out var modulus)) return false;
        var rsaParams = new RSAParameters {
            Exponent = exponent.ToByteArray(isUnsigned: true, isBigEndian: true),
            Modulus = modulus.ToByteArray(isUnsigned: true, isBigEndian: true)
        };
        rsa = RSA.Create(rsaParams);
        return true;
    }
    public bool TryParse(byte[] bytes, [NotNullWhen(true)] out IPublicKey? key) {
        key = null;
        if (!TryParseRsa(bytes, out var rsa)) return false;
        key = new RsaPublicKey(rsa);
        return true;
    }
    public HashAlgorithm HashAlgorithm => SHA256.Create();
}

