namespace ZipZap.Front.Factories;
public interface IFactory<out T, in TConfiguration>{
    public T Create(TConfiguration configuration);
}
