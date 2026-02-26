// NoEncryptionAlgorithm.cs - Part of the ZipZap project for storing files online
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
using System.Threading;
using System.Threading.Tasks;

namespace ZipZap.Sftp.Ssh.Algorithms;

public class NoEncryptionAlgorithm : IEncryptionAlgorithm {
    public NameList.Item Name => new NameList.GlobalName("none");

    public bool OverridesMac => false;
    public int IVLength => 0;
    public int KeyLength => 0;

    public IDecryptor GetDecryptor(Stream stream, byte[] IV, byte[] Key, IMacValidator mac) {
        if (IV.Length != IVLength) throw new ArgumentException($"{nameof(IV)} should be of length {IVLength}");
        if (Key.Length != KeyLength) throw new ArgumentException($"{nameof(Key)} should be of length {KeyLength}");
        return new NoEncryptionDecryptor(stream, mac);
    }

    public IEncryptor GetEncryptor(Stream stream, byte[] IV, byte[] Key, IMacGenerator mac) {
        if (IV.Length != IVLength) throw new ArgumentException($"{nameof(IV)} should be of length {IVLength}");
        if (Key.Length != KeyLength) throw new ArgumentException($"{nameof(Key)} should be of length {KeyLength}");
        return new NoEncryptionEncryptor(stream,mac);
    }
    private class NoEncryptionDecryptor : IDecryptor {
        private readonly Stream _stream;
        private readonly IMacValidator _macValidator;

        public uint MacSequential => _macValidator.GetCount();

        public NoEncryptionDecryptor(Stream stream, IMacValidator macValidator) {
            _stream = stream;
            _macValidator = macValidator;
        }

        public async Task<Packet?> ReadPacket(CancellationToken cancellationToken) {
            /* 6 = sizeof(byte padding) + sizeof(byte msgtype) + min 4 bytes padding*/
            static bool isValidPacketLength(uint length)
                => length >= 6 && (length + 4) % 8 == 0;
            if (await _stream.SshTryReadUint32(cancellationToken) is not uint length || !isValidPacketLength(length))
                return null;
            /*padding MUST be at least 4 bytes*/
            if (await _stream.SshTryReadByte(cancellationToken) is not byte paddingLength || length < 4)
                return null;
            var payload = new byte[length - paddingLength - sizeof(byte)];
            if (!await _stream.SshTryReadArray(payload, cancellationToken)) return null;
            var padding = new byte[paddingLength];
            if (!await _stream.SshTryReadArray(padding, cancellationToken)) return null;
            var packet = new Packet(payload, padding);
            var mac = new byte[_macValidator.MacLength];
            if (!await _stream.SshTryReadArray(mac, cancellationToken)
                || !await _macValidator.Validate(packet, mac, cancellationToken))
                return null;

            return packet;
        }
    }

    private class NoEncryptionEncryptor : IEncryptor {
        private readonly IMacGenerator _macGenerator;
        private readonly Stream _stream;

        public NoEncryptionEncryptor(Stream stream, IMacGenerator macGenerator) {
            _stream = stream;
            _macGenerator = macGenerator;
        }

        public uint MacSequential => _macGenerator.GetCount();

        public async Task SendPacket<T>(T serverPayload, CancellationToken cancellationToken)
        where T : IServerPayload {
            var packet = serverPayload.ToPacket(8);
            var mac = await _macGenerator.Generate(packet, cancellationToken);
            var output = new byte[4 + packet.Length + mac.Length];
            packet.WriteTo(output);
            mac.CopyTo(output, packet.Length);
            await _stream.SshWriteArray(output,cancellationToken);
        }
    }
}
