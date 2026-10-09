// 풀에서 꺼내고 되돌릴 때 알림을 받는 컴포넌트 — 'PrefabPool<T>'가 부른다.
//
// ■ 언제 무엇을 하나
//   OnRent   : 켜진 직후. 표시 상태(알파·스케일·색)를 처음 값으로 되돌린다
//   OnReturn : 꺼지기 직전. 진행 중인 트윈을 Kill하고, 들고 있던 데이터·구독을 비운다
//
// ⚠️ 비동기 작업은 'PooledObject.RentToken'을 받아서 돌린다. 풀링된 오브젝트는 파괴되지 않으므로
//   'GetCancellationTokenOnDestroy'는 반납 때 발동하지 않는다 — 반납 뒤에도 작업이 계속 돈다.
public interface IPoolable
{
    void OnRent();

    void OnReturn();
}
