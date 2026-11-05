using QFramework;

namespace GameFish
{
    public class GameFishArchitecture : Architecture<GameFishArchitecture>
    {
        protected override void Init()
        {
            RegisterModel(new PlayerModel());
            RegisterUtility(new JsonStorageUtility());
        }
    }
}
