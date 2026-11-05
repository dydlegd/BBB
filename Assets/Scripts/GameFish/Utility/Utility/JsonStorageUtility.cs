using QFramework;

namespace GameFish
{
    public class JsonStorageUtility : IUtility
    {
        public bool SaveData(string data, string filePath)
        {
            return JsonUtil.WriteToFile(data, filePath, true);
        }
        
        public T LoadData<T>(string filePath)
        {
            return JsonUtil.ReadFromFile<T>(filePath);
        }
    }
}
