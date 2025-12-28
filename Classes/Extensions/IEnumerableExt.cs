using System;
using System.Collections.Generic;
using System.Linq;

using ZipZap.Classes.Helpers;

namespace ZipZap.Classes.Extensions;

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
