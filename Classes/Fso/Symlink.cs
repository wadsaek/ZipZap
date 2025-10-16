namespace ZipZap.Classes;

sealed public class Symlink : Fso {
    public Symlink() {
        Target = null!;
    }
    public Symlink(
        FsoId id,
        FsData data,
        string target
        ) : base(id, data) {
        Target = target;
    }

    public string Target { get; set; }
}
