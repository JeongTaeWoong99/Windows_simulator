using System;

// ※ 서버(Assets/Scripts/Utils)의 동일 유틸을 클라이언트가 독립적으로 소유하기 위해 복사한 사본.
//   클라이언트에서 자유롭게 수정/제거할 수 있도록 namespace 를 Client.Utils 로 둔다.
namespace Client.Utils
{
    public abstract class Singleton<T> where T : Singleton<T>, new()
    {
        private static readonly Lazy<T> _instance = new Lazy<T>(() => new T());
        public static T Instance => _instance.Value;
    }
}
