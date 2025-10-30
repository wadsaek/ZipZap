using System.Text;

namespace ZipZap.Classes.Extensions;

public static class StringBuilderExt {
    extension(StringBuilder builder) {
        public void RemoveLastCharacters(int n = 1) {

            builder.Remove(builder.Length - n, n);
        }
    }
}
