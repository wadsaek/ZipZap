// SshPublicKey.cs - Part of the ZipZap project for storing files online
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
using System.Security.Cryptography;

using ZipZap.Sftp.Ssh.Algorithms;

namespace ZipZap.Classes;


public sealed record SshPublicKey(string Value);

public enum SshKeyFormat {
    Pem,
    OpenSsh
}

public static class SshExt {
    private interface IVerifier {
        bool VerifySignature(byte[] signature, byte[] data);
        byte[] ToSshByteString();
    }
    private sealed record RsaVerifier(RSA Rsa) : IVerifier {
        public byte[] ToSshByteString()
            => new RsaPublicKeyAlgorithm.RsaPublicKey(Rsa).ToByteString();

        public bool VerifySignature(byte[] signature, byte[] data)
            => new RsaPublicKeyAlgorithm.RsaPublicKey(Rsa).Verify(signature, data);
    }
    extension(SshPublicKey key) {
        public VerifySignatureResult VerifySignature(byte[] signature, byte[] data) {
            var maybeVerified = key.ToVerifier()?.VerifySignature(signature, data);
            return maybeVerified switch {
                true => VerifySignatureResult.Ok,
                false => VerifySignatureResult.BadSignature,
                null => VerifySignatureResult.BadKey
            };
        }
#pragma warning disable CA1859 // Use concrete types when possible for improved performance
        private IVerifier? ToVerifier() {
            if (PemEncoding.TryFind(key.Value, out var fields)
                && key.Value[fields.Label]
                    .Contains("rsa", StringComparison.OrdinalIgnoreCase)) {
                try {

                    var rsa = RSA.Create();
                    rsa.ImportFromPem(key.Value);
                    return new RsaVerifier(rsa);
                } catch {
                    return null;
                }
            }
            if (key.Value.StartsWith("ssh-rsa")) {
                var split = key.Value.Split(' ');
                if (split.Length < 2) return null;
                var base64 = split[1];
                var bytes = Convert.FromBase64String(base64);
                if (!RsaPublicKeyAlgorithm.TryParseRsa(bytes, out var rsa)) return null;
                return new RsaVerifier(rsa);
            }
            return null;
        }
#pragma warning restore CA1859 // Use concrete types when possible for improved performance
        public byte[]? ToSshByteString() {
            var verifier = key.ToVerifier();
            return verifier?.ToSshByteString();
        }
    }
}
public enum VerifySignatureResult {
    Ok,
    BadSignature,
    BadKey
}
