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

using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace ZipZap.Sftp.Ssh.Algorithms;

// aes128-gcm as specified by rfc5468 <https://www.rfc-editor.org/rfc/rfc5647>
// and "Fixed AES-GCM modes for the SSH protocol"
// <https://www.ietf.org/archive/id/draft-miller-sshm-aes-gcm-00.html>
internal class Aes128GcmEncryptionAlgorithm : IEncryptionAlgorithm {
    public NameList.Item Name => new NameList.LocalName("aes128-gcm", "openssh.com");

    public bool OverridesMac => true;

    public int IVLength => 12;

    public int KeyLength => 16;

    public IDecryptor GetDecryptor(Stream stream, byte[] IV, byte[] Key, IMacValidator mac) {
        if (IV.Length != IVLength) throw new ArgumentException($"{nameof(IV)} should be of length {IVLength}");
        if (Key.Length != KeyLength) throw new ArgumentException($"{nameof(Key)} should be of length {KeyLength}");
        return new Aes128GcmDecryptor(stream, mac, IV, Key);
    }

    public IEncryptor GetEncryptor(byte[] IV, byte[] Key, IMacGenerator mac) {
        if (IV.Length != IVLength) throw new ArgumentException($"{nameof(IV)} should be of length {IVLength}");
        if (Key.Length != KeyLength) throw new ArgumentException($"{nameof(Key)} should be of length {KeyLength}");
        return new Aes128GcmEncryptor(mac, IV, Key);
    }

    private class Aes128GcmDecryptor : IDecryptor {
        private readonly Stream _stream;
        private readonly IMacValidator _macValidator;
        private readonly byte[] _key;

        private readonly uint _fixed;
        private ulong _incrementing;

        public Aes128GcmDecryptor(Stream stream, IMacValidator macValidator, byte[] IV, byte[] key) {
            _stream = stream;
            _macValidator = macValidator;
            _key = key;
            var ivstream = new MemoryStream(IV);
            ivstream.SshTryReadUint32Sync(out _fixed);
            ivstream.SshTryReadUInt64Sync(out _incrementing);
        }

        public uint MacSequential => _macValidator.GetCount();

        public async Task<Packet?> ReadPacket(CancellationToken cancellationToken) {
            _macValidator.IncrementCounter();
            // the packet is encoded as a concatenation of length, encrypted data
            // and mac, which is the same as `string encrypted_data, byte[n] mac`
            if (await _stream.SshTryReadByteString(cancellationToken) is not byte[] encrypted) return null;
            var mac = new byte[16];
            if (!await _stream.SshTryReadArray(mac, cancellationToken)) return null;
            var decryptor = new AesGcm(_key, mac.Length);
            byte[] nonce;
            unchecked {
                nonce = new SshMessageBuilder()
                    .Write(_fixed)
                    .Write(_incrementing++)
                    .Build();
            }
            var plaintext = new byte[encrypted.Length];
            var associated = new SshMessageBuilder()
                .Write(encrypted.Length)
                .Build();
            try {
                decryptor.Decrypt(nonce, encrypted, mac, plaintext, associated);
#if NET8_0_OR_GREATER
            } catch (AuthenticationTagMismatchException e) {
#else
            } catch (CryptographicException e){
#endif
                return null;
            }
            var packetStream = new MemoryStream(plaintext);
            if (!packetStream.SshTryReadByteSync(out var paddingLength)) return null;
            var payload = new byte[plaintext.Length - 1 - paddingLength];
            if (!packetStream.SshTryReadArraySync(payload)) return null;
            var padding = new byte[paddingLength];
            if (!packetStream.SshTryReadArraySync(padding)) return null;

            return new(payload, padding);
        }
    }

    private class Aes128GcmEncryptor : IEncryptor {
        private readonly IMacGenerator _mac;
        private readonly byte[] _key;

        private readonly uint _fixed;
        private ulong _incrementing;

        public Aes128GcmEncryptor(IMacGenerator mac, byte[] iv, byte[] key) {
            _mac = mac;
            _key = key;
            var ivstream = new MemoryStream(iv);
            ivstream.SshTryReadUint32Sync(out _fixed);
            ivstream.SshTryReadUInt64Sync(out _incrementing);
        }

        public uint MacSequential => throw new NotImplementedException();

        public Task<byte[]> EncryptPacket<TPayload>(TPayload serverPayload, CancellationToken cancellationToken) where TPayload : IServerPayload {
            _mac.IncrementCounter();

            var packet = serverPayload.ToPacket(16, 4);
            var plaintext = new SshMessageBuilder()
                .Write(packet.PaddingLength)
                .WriteArray(packet.Payload)
                .WriteArray(packet.Padding)
                .Build();
            var associatedData = new SshMessageBuilder()
                .Write(packet.Length)
                .Build();

            byte[] nonce;
            unchecked {
                nonce = new SshMessageBuilder()
                    .Write(_fixed)
                    .Write(_incrementing++)
                    .Build();
            }

            var encrypted = new byte[plaintext.Length];
            var mac = new byte[16];
            new AesGcm(_key, mac.Length).Encrypt(nonce, plaintext, encrypted, mac, associatedData);

            var result = new SshMessageBuilder()
                .Write(packet.Length)
                .WriteArray(encrypted)
                .WriteArray(mac)
                .Build();
            return Task.FromResult(result);
        }
    }
}
