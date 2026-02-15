// NameList.cs - Part of the ZipZap project for storing files online
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

using System.Linq;
using System.Text;

using ZipZap.LangExt.Extensions;

namespace ZipZap.Sftp.Ssh;

public record NameList(NameList.Item[] Names) {
    public override string ToString()
        => Names
            .Select(n => n.ToString())
            .ConcatenateWith(",");

    public abstract record Item(string Name) {
        public static bool TryParse(string raw, out Item name) {
            name = raw.Split("@") switch {
                [var global] => new GlobalName(global),
                [var local, var domain] => new LocalName(local, domain),
                _ => null!
            };
            return name is not null;
        }
    }
    public sealed record GlobalName(string Name) : Item(Name) {
        public override string ToString() => Name;
    }
    public sealed record LocalName(string Name, string Domain) : Item(Name) {
        public override string ToString() => $"{Name}@{Domain}";
    }
}

public static class NameListExt {
    extension(NameList nameList) {
        public byte[] ToByteString() {
            var str = nameList.ToString();
            return Encoding.ASCII.GetBytes(str);
        }
    }
}
