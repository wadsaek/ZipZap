// PathHelper.cs - Part of the ZipZap project for storing files online
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

using System.Collections.Generic;
using System.Linq;

using ZipZap.Classes.Extensions;

namespace ZipZap.Front.Helpers;

static class PathHelper {

    public static IEnumerable<string> NormalizePath(string target, IEnumerable<string> parts) {
        List<string> partsMut = parts.ToList();
        var targetParts = target.SplitPath();
        foreach (var part in targetParts) {
            switch (part) {
                case "." or "":
                    break;
                case "..":
                    partsMut.RemoveAt(partsMut.Count - 1);
                    break;
                case var p:
                    partsMut.Add(p);
                    break;
            }
        }
        return partsMut;
    }
}
