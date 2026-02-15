// Configuration.cs - Part of the ZipZap project for storing files online
//     Copyright (C) 2026  Barenboim Esther wadsaek@gmail.com
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY

using ZipZap.Classes;

namespace ZipZap.FileService.Helpers;

public class Configuration : IConfiguration {
    public string BaseFilePath { get; init; }
    public FileSize MaximumFileSize { get; init; }

    public Configuration(
        string baseFilePath,
        FileSize maximumFileSize) {
        BaseFilePath = baseFilePath;
        MaximumFileSize = maximumFileSize;
    }
}
