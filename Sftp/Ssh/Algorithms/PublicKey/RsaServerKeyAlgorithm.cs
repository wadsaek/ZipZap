using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace ZipZap.Sftp.Ssh.Algorithms;

public class RsaServerKeyAlgorithm : IServerHostKeyAlgorithm {
    private readonly RsaPublicKeyAlgorithm _inner;
    private readonly ISftpConfiguration _sftpConfiguration;

    public RsaServerKeyAlgorithm(RsaPublicKeyAlgorithm inner, ISftpConfiguration sftpConfiguration) {
        _inner = inner;
        _sftpConfiguration = sftpConfiguration;
    }

    public NameList.Item Name => _inner.Name;

    class RsaPublicKeyPair : IHostKeyPair {
        private readonly RSA _rsaKey;
        private readonly HashAlgorithm _hashAlgorithm;
        const string ID = "rsa-sha2-256";

        public RsaPublicKeyPair(RSA rsaKey, HashAlgorithm hashAlgorithm) {

            _rsaKey = rsaKey;
            _hashAlgorithm = hashAlgorithm;
        }

        public async Task<byte[]> GetPublicKeyBytes(CancellationToken cancellationToken) {
            // NOTE:
            // these are encoded as unsigned big-endian.
            // You can verify this through trial and error.
            // There is 0 reason Microsoft can't document it.
            // There is even less reason for this to be the EXACT OPPOSITE of
            // the default BigInteger constructor's parameters.
            var parameters = _rsaKey.ExportParameters(false);
            var modulus = new BigInteger(parameters.Modulus, isUnsigned: true, isBigEndian: true);
            var exponent = new BigInteger(parameters.Exponent, isUnsigned: true, isBigEndian: true);
            return new SshMessageBuilder()
                .Write(ID)
                .Write(exponent)
                .Write(modulus)
                .Build();

        }

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

    public byte[] Sign(byte[] unsigned, byte[] key) {
        throw new System.NotImplementedException();
    }

    public bool Verify(byte[] unsigned, byte[] key) => _inner.Verify(unsigned, key);

}

