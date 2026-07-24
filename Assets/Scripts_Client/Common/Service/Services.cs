using System;
using System.Collections.Generic;

// 역할 인터페이스 ↔ 구현 인스턴스를 등록/조회하는 경량 서비스 로케이터.
// 하드 싱글톤(X.Inst)을 대체해, 호출자가 구체 클래스가 아닌 '역할'에만 의존하게 한다 (DIP).
public static class Services
{
    private static readonly Dictionary<Type, object> _services = new();

    // 구현을 역할 타입 T로 등록한다 (각 MonoService가 Awake에서 자신을 등록)
    public static void Register<T>(T service) where T : class
    {
        _services[typeof(T)] = service;
    }

    // 등록 해제 — 현재 등록된 인스턴스가 service일 때만 제거한다 (씬 재로드 시 잔존 참조 방지)
    public static void Unregister<T>(T service) where T : class
    {
        if (_services.TryGetValue(typeof(T), out object current) && ReferenceEquals(current, service))
        {
            _services.Remove(typeof(T));
        }
    }

    // 역할 T의 구현을 가져온다 (미등록 시 예외 — 초기화 순서 버그를 즉시 드러낸다)
    public static T Get<T>() where T : class
    {
        return (T)_services[typeof(T)];
    }
}
