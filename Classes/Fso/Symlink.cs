namespace ZipZap.Classes;

sealed public class Symlink : Fso {
    public Symlink() {
        Target = null!;
    }
    public Symlink(
        FsoId id,
        string name,
        FsData data,
        string target
        ) : base(id, name, data) {
        Target = target;
    }

    public string Target { get; set; }
}
