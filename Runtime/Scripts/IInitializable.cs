public interface IInitializable
{ 
    void Init();
}
public interface IInitializable<D> 
{ 
    void Init(D data);
}