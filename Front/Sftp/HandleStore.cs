// HandleStore.cs - Part of the ZipZap project for storing files online
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
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace ZipZap.Front.Sftp;
using ZipZap.Sftp;

class HandleStore {

    readonly Dictionary<Handle, OpenFileData> _openHandles = [];

    public Handle CreateHandle(OpenFileData data) {
        var handleid = Guid.NewGuid();
        Handle handle;
        do
            handle = new(handleid.ToString());
        while (_openHandles.ContainsKey(handle));
        _openHandles.Add(handle, data);
        return handle;
    }
    public bool TryGetFileData(Handle handle, [NotNullWhen(true)] out OpenFileData.FileData? data) {

        data = null;
        if (!_openHandles.TryGetValue(handle, out var dataRaw)
            || dataRaw is not OpenFileData.FileData fileData) return false;
        data = fileData;
        return true;
    }
    public bool TryGetDirData(Handle handle, [NotNullWhen(true)] out OpenFileData.DirectoryData? data){
        data = null;
        if (!_openHandles.TryGetValue(handle, out var dataRaw)
            || dataRaw is not OpenFileData.DirectoryData dirData) return false;
        data = dirData;
        return true;
    }
    public bool Remove(Handle handle) => _openHandles.Remove(handle);
    public OpenFileData this[Handle handle] {set => _openHandles[handle] = value;}
}
