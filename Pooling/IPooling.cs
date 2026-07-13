namespace qtLib.Pooling
{
    public interface IInterfacePooling<TInterface> where TInterface : IPoolingObject
    {
        public TInterface Get(TInterface target);
        
        public void Release(TInterface target);
    }

    public interface IIDPooling<TObject> where TObject : IPoolingObject
    {
        public TObject Get(int ID);
        public void Release(TObject target);
    }
}