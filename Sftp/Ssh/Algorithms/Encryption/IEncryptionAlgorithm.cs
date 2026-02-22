// IEncryptionAlgorithm.cs - Part of the ZipZap project for storing files online
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
using System.Threading;
using System.Threading.Tasks;

namespace ZipZap.Sftp.Ssh.Algorithms;

public interface IEncryptionAlgorithm : INamed {
    public bool OverridesMac { get; }
    public int IVLength { get; }
    public int KeyLength { get; }

    ///<returns>A stateful encryptor</returns>
    public IEncryptor GetEncryptor(byte[] IV, byte[] Key, IMacGenerator mac);

    ///<returns>A stateful decryptor</returns>
    public IDecryptor GetDecryptor(Stream stream, byte[] IV, byte[] Key, IMacValidator mac);
}


public interface IDecryptor {
    public Task<Packet?> ReadPacket(CancellationToken cancellationToken);
    public uint MacSequential { get; }
}

public interface IEncryptor {
    // INFO: I'm not sure whether i want to create a packet
    // with the correct padding, send it to the encryptor,
    // and then ensure that the padding is indeed correct.
    // This sounds like it separates responsibilities a little better,
    // but this also means lots of double checks... I don't want those.
    // In any case, the signature may change to the following later:
    // ```cs
    // public Task<byte[]> EncryptPacket(Packet packet, CancellationToken cancellationToken);
    // ```
    public Task<byte[]> EncryptPacket<TPayload>(TPayload serverPayload, CancellationToken cancellationToken)
    where TPayload : IServerPayload;

    public uint MacSequential { get; }
}
