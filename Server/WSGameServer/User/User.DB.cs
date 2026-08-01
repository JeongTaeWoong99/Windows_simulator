namespace WSGameServer;

public partial class User
{
    /// <summary>
    /// 로그인 조회 결과(Row)를 도메인으로 변환해 적재하고 로그인을 마무리한다(로직 스레드).
    ///
    /// <para>
    /// <b>Row가 들어오는 것은 여기(User의 partial)까지다.</b> 순수 코어(Inventory·Wallet·WorkStation)에는
    /// 도메인 객체만 넘긴다 — 코어가 Repository 타입을 참조하면 의존 방향이 뒤집힌다.
    /// </para>
    /// </summary>
    public void OnLoginDataLoaded(PlayerLoginData data)
    {
        // ⚠️ 채취는 "지금부터" 시작한다. 로그아웃 시각을 읽어 그 구간을 정산하면
        //    오프라인 진행이 되살아난다(게임기획코어 P2 재정의 — 접속 중에만 자란다).
        //    진행 조각도 이월하지 않으므로 매 접속이 0에서 출발한다.
        var startedAt = DateTime.UtcNow;

        LoadInventory(data.InventoryRows);
        LoadCurrencies(data.CurrencyRows);

        // 캐릭터가 슬롯 속도의 근거이므로 슬롯보다 먼저 적재한다.
        LoadCharacters(data.CharacterRows);
        LoadWorkStation(data.WorkStationSlotRows, startedAt);

        // 배치된 캐릭터의 적성으로 각 슬롯의 속도를 맞춘다.
        // 접속마다 다시 계산하므로 그동안 밸런스가 바뀌었어도 반영된다.
        RefreshWorkStationSpeed(notify: false);

        Login();   // S_LoginResponse + 인벤·재화·슬롯 스냅샷 전송
    }
}
