namespace MirrorDimension.Compat;

public interface IModCompat
{
    public virtual string ModName => GetType().Name;
    public abstract void Initialize();
}
