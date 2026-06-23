#if UNITY_5_3_OR_NEWER

// Pure C#
namespace MikaNetwork.Unity.Utils
{
    public abstract class Singleton<T> where T : Singleton<T>, new()
    {
        private static T _instance = new T();
        public static T Instance => _instance;
    }
}

#endif