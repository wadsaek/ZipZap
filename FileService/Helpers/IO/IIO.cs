// IIO.cs - Part of the ZipZap project for storing files online
//     Copyright (C) 2026  Barenboim Esther wadsaek@gmail.com
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace ZipZap.FileService.Helpers;

public interface IIO {
    public Task WriteAsync(string path, Stream content);
    public Task<Stream> ReadAsync(string path);
    public Task RemoveAsync(string path);
    public Task RemoveRangeAsync(IEnumerable<string> paths);
    public Task CopyAsync(string oldPath, string newPath);
    public bool IsValidPathChar(char c);
    public bool IsValidPath(string path);
    public Task<bool> PathExistsAsync(string path);
}
