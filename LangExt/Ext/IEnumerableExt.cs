// IEnumerableExt.cs - Part of the ZipZap project for storing files online
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
using System.Linq;

using ZipZap.LangExt.Helpers;

namespace ZipZap.LangExt.Extensions;

public static class EnumerableExt {
    extension(IEnumerable<string> strings) {
        public string ConcatenateWith(string str) => strings.ToList() switch {
            [] => "",
            var list => list.Aggregate((acc, next) => $"{acc}{str}{next}")
        };
    }
    extension<T>(IEnumerable<T> enumerable) {
        public IEnumerable<T> Assert(Func<T, bool> predicate) {
            foreach (var t in enumerable) {
                Assertions.Assert(predicate(t));
                yield return t;
            }
        }
        public IEnumerable<T> WhereNot(Func<T, bool> predicate)
            => enumerable.Where(x => !predicate(x));
    }
    extension<T>(IEnumerable<IEnumerable<T>> enumerable) {
        public IEnumerable<T> Flatten() => enumerable.SelectMany(i => i);
    }
}
