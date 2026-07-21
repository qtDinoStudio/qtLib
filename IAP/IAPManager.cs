using qtLib.Singleton;

namespace qtLib.IAP
{
    public partial class IAPManager : qtSingleton<IAPManager>
    {
        public class IAPProduct
        {
            //Shop
            public const string Shop_RemoveAds = "com.qtdino.match3.stack.pop.removeads";
            public const string Shop_KeepPlayingPack = "com.qtdino.match3.stack.pop.keepplayingpack";
        }
    }
}