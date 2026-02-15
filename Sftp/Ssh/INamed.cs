// INamed.cs - Part of the ZipZap project for storing files online
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
using System.Linq;

namespace ZipZap.Sftp.Ssh;

public interface INamed {
    public NameList.Item Name { get; }
}

public static class INamedExt {
    extension(IEnumerable<INamed> named) {
        public NameList ToNameList() => new(named.Select(n => n.Name).ToArray());
    }
}
