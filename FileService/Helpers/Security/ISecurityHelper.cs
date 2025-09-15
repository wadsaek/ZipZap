using System;

namespace ZipZap.FileService.Helpers;

public interface ISecurityHelper{
    public string GenerateString(int length,Func<char, bool> isValidChar);
}
