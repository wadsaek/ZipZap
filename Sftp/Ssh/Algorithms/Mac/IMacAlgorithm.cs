// IMacAlgorithm.cs - Part of the ZipZap project for storing files online
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

using System.Threading;
using System.Threading.Tasks;

namespace ZipZap.Sftp.Ssh.Algorithms;

public interface IMacAlgorithm : INamed {
    public int Length { get; }
    public bool IsEncryptThenMac { get; }
    public int KeyLength { get; }

    public IMacGenerator CreateGenerator(uint sequential, byte[] IntegrityKey);
    public IMacValidator CreateValidator(uint sequential, byte[] IntegrityKey);
}

public interface IMacValidator {
    public void IncrementCounter();
    public uint GetCount();
    public Task<bool> Validate(Packet packet, byte[] mac, CancellationToken cancellationToken);

    public int MacLength { get; }
}

public interface IMacGenerator {
    public void IncrementCounter();
    public uint GetCount();
    public Task<byte[]> Generate(Packet packet, CancellationToken cancellationToken);

    public int MacLength { get; }
}
