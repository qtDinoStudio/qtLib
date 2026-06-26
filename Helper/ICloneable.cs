namespace qtLib.Helper
{
    public interface ICloneable<T> where T : class
    {
        public T Clone();
    }
}