// Concrete.cs - Part of the ZipZap project for storing files online
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

namespace ZipZap.Sftp.Ssh.Algorithms;

using System;
using System.Numerics;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

public class DiffieHelmanGroup14Sha256 : IKeyExchangeAlgorithm {
    public static BigInteger Group { get; } = BigInteger.Parse("00FFFFFFFFFFFFFFFFC90FDAA22168C234C4C6628B80DC1CD129024E088A67CC74020BBEA63B139B22514A08798E3404DDEF9519B3CD3A431B302B0A6DF25F14374FE1356D6D51C245E485B576625E7EC6F44C42E9A637ED6B0BFF5CB6F406B7EDEE386BFB5A899FA5AE9F24117C4B1FE649286651ECE45B3DC2007CB8A163BF0598DA48361C55D39A69163FA8FD24CF5F83655D23DCA3AD961C62F356208552BB9ED529077096966D670C354E4ABC9804F1746C08CA18217C32905E462E36CE3BE39E772C180E86039B2783A2EC07A28FB5C55DF06F4C52C9DE2BCBF6955817183995497CEA956AE515D2261898FA051015728E5A8AACAA68FFFFFFFFFFFFFFFF", System.Globalization.NumberStyles.HexNumber);
    public static int Generator { get; } = 2;
    public NameList.Item Name => new NameList.GlobalName("diffie-hellman-group14-sha256");

    public Func<byte[], byte[]> Hash => SHA256.HashData;

    Task<KeyExchangeResult?> IKeyExchangeAlgorithm.ExchangeKeysAsync(IHostKeyPair hostKeyPair,KexInitPair kexInitPair, KeyExchangeInput input, CancellationToken cancellationToken) {
        return new DiffieHelman(Group, Generator, Hash).ExchangeKeysAsync(hostKeyPair,kexInitPair, input, cancellationToken);
    }
}
