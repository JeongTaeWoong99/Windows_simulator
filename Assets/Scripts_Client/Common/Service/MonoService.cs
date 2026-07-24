using UnityEngine;

// 자기 자신을 역할 인터페이스 T로 Services에 자동 등록/해제하는 MonoBehaviour 베이스.
// 매니저/시스템/컨트롤러의 'X.Inst + Awake{Inst=this}' 보일러플레이트를 통일한다.
// 주의: Awake에서는 다른 서비스를 Get 하지 않는다(모든 Awake 등록 완료 후 Start에서 사용).
public abstract class MonoService<T> : MonoBehaviour where T : class
{
    protected virtual void Awake()
    {
        Services.Register<T>(this as T);
    }

    protected virtual void OnDestroy()
    {
        Services.Unregister<T>(this as T);
    }
}
