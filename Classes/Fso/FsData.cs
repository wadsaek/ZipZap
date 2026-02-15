// FsData.cs - Part of the ZipZap project for storing files online
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

using ZipZap.Classes.Helpers;

namespace ZipZap.Classes;

public record FsData(
    MaybeEntity<Directory, FsoId>? VirtualLocation,
    Permissions Permissions,
    string Name,
    Ownership Ownership
);

public record Ownership(
    int FsoOwner,
    int FsoGroup
) {
    public override string ToString() => $"{FsoOwner}:{FsoGroup}";
}
