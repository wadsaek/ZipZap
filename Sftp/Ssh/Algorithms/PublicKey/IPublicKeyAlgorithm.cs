// IPublicKeyAlgorithm.cs - Part of the ZipZap project for storing files online
//     Copyright (C) 2026  Barenboim Esther wadsaek@gmail.com
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY

using System.Security.Cryptography;

namespace ZipZap.Sftp.Ssh.Algorithms;

public interface IPublicKeyAlgorithm : INamed {
    public bool Verify(byte[] unsigned, byte[] key);
}

public class RsaPublicKeyAlgorithm : IPublicKeyAlgorithm {
    public NameList.Item Name => new NameList.GlobalName("rsa-sha2-256");
    public HashAlgorithm HashAlgorithm => SHA256.Create();

    public bool Verify(byte[] unsigned, byte[] key) {
        throw new System.NotImplementedException();
    }
}
