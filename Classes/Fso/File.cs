namespace ZipZap.Classes;

sealed public class File : Fso {

    public File() {
        PhysicalPath = null!;
    }

    public File(
        FsoId id,
        FsData data,
        string dataPath
        ) : base(id, data) {
        PhysicalPath = dataPath;
    }

    public string PhysicalPath { get; set; }
}
