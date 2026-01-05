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
        public sealed record GlobalName(string Name) : Item(Name) {
            override public string ToString() => Name;
        }
        public sealed record LocalName(string Name, string Domain) : Item(Name) {
            override public string ToString() => $"{Name}@{Domain}";
        }
        public static bool TryParse(string raw, out Item name) {
            name = raw.Split("@") switch {
                [var global] => new GlobalName(global),
                [var local, var domain] => new LocalName(local, domain),
                _ => null!
            };
            return name is not null;
        }
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
