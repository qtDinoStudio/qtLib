using Cysharp.Threading.Tasks;

namespace qtLib.Singleton
{
    public interface IManualInit
    {
        public UniTask ManualInit();
    }
}