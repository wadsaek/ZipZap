// FsoExt.cs - Part of the ZipZap project for storing files online
//     Copyright (C) 2026  Barenboim Esther wadsaek@gmail.com
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY

using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Google.Protobuf;

using ZipZap.Classes.Adapters;
using ZipZap.Grpc;
namespace ZipZap.Classes.Extensions;

public static class FsoExt {
    extension(Fso fso) {
        public FsoSharedData ToRpcSharedData() => new() {
            Name = fso.Data.Name,
            Ownership = fso.Data.Ownership.ToGrpcOwnership(),
            Permissions = (int)fso.Data.Permissions.Inner,
            RootId = (fso.Data.VirtualLocation
                ?.Id
                ?? fso.Id)
                .Value.ToGrpcGuid()
        };
    }
    extension(File file) {
        public static async Task<FileData> ToRpcFileDataAsync(Stream stream) {
            var data = FileData.NewFileData(
                await ByteString.FromStreamAsync(stream)
            );
            return data;
        }
        public async Task<(FsoSharedData, FileData)> ToRpcResponse(Stream stream) => (file.ToRpcSharedData(), await File.ToRpcFileDataAsync(stream));
    }
    extension(Directory dir) {
        public DirectoryData ToRpcDirectoryData() {
            var data = new DirectoryData();
            data.Entries.Add(
                dir.MaybeChildren
                    .Select(fso => fso.ToFsoWithType())
           );
            return data;
        }
        public (FsoSharedData, DirectoryData) ToRpcResponse() => (dir.ToRpcSharedData(), dir.ToRpcDirectoryData());
    }
    extension(Symlink link) {
        public SymlinkData ToRpcLinkData() {
            var data = SymlinkData.NewSymlinkData(
                    link.Target
            );
            return data;
        }
        public (FsoSharedData, SymlinkData) ToRpcResponse() => (link.ToRpcSharedData(), link.ToRpcLinkData());
    }
}
