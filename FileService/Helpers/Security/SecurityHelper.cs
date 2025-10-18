using System;
using System.Linq;

using ZipZap.Classes.Helpers;

namespace ZipZap.FileService.Helpers;

using static Assertions;

public class SecurityHelper : ISecurityHelper {
    public string GenerateString(int length, Func<char, bool> isValidChar) {
        Assert(isValidChar('_'), "the byte value of `_` has to be a valid byte");

        var rnd = new Random();
        byte[] bytes = new byte[length];
        rnd.NextBytes(bytes);
        char[] chars = bytes
            .Select(b => 0b01111111 & b) // if the most significant bit is one, a character is not valid ascii
            .Select(b => (char)b)
            .Select(c => isValidChar(c) ? c : '_')
            .ToArray();

        return new string(chars);
    }
}
