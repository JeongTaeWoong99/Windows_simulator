#if UNITY_5_3_OR_NEWER

using UnityEngine;

// Unity MonoBehaviour Script
// ※ 서버(Assets/Scripts/Utils)의 동일 유틸을 클라이언트가 독립적으로 소유하기 위해 복사한 사본.
//   클라이언트에서 자유롭게 수정/제거할 수 있도록 namespace 를 Client.Utils 로 둔다.
namespace Client.Utils
{
    public abstract class SingletonMonoBehaviour<T> : MonoBehaviour where T : SingletonMonoBehaviour<T>
    {
        private static T? _instance;

        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<T>();
                    if (_instance == null)
                    {
                        GameObject obj = new GameObject(typeof(T).Name);
                        _instance = obj.AddComponent<T>();
                        DontDestroyOnLoad(obj);
                    }
                }
                return _instance;
            }
        }

        protected virtual void Awake()
        {
            if (_instance == null)
            {
                _instance = this as T;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }
    }
}

#endif
