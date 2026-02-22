// NoMacAlgorithm.cs - Part of the ZipZap project for storing files online
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
using System.Threading;
using System.Threading.Tasks;

namespace ZipZap.Sftp.Ssh.Algorithms;

public class NoMacAlgorithm : IMacAlgorithm {
    public int Length { get; }

    public NameList.Item Name => new NameList.GlobalName("none");

    // this doesn't really matter
    public bool IsEncryptThenMac => true;

    public int KeyLength => 0;


    public IMacGenerator CreateGenerator(uint sequential, byte[] integrityKey) {
        if (integrityKey.Length != KeyLength)
            throw new ArgumentException($"Length of {nameof(integrityKey)} is not equal to {KeyLength}");

        return new NoMacHandler(sequential);
    }

    public IMacValidator CreateValidator(uint sequential, byte[] integrityKey) {
        if (integrityKey.Length != KeyLength)
            throw new ArgumentException($"Length of {nameof(integrityKey)} is not equal to {KeyLength}");

        return new NoMacHandler(sequential);
    }

    private class NoMacHandler : IMacGenerator, IMacValidator {
        uint _sequential = 0;

        public NoMacHandler(uint sequential) {
            _sequential = sequential;
        }

        public int MacLength => 0;

        public Task<byte[]> Generate(Packet packet, CancellationToken cancellationToken) {
            IncrementCounter();
            return Task.FromResult(Array.Empty<byte>());
        }

        public uint GetCount() => _sequential;
        public void IncrementCounter() {
            unchecked {
                _sequential++;
            }
        }

        public Task<bool> Validate(Packet packet, byte[] mac, CancellationToken cancellationToken) {
            IncrementCounter();
            return Task.FromResult(mac.SequenceCompareTo([]) == 0);
        }
    }
}

