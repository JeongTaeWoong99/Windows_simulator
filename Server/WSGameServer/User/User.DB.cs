using GameData;
using MikaProtocol;
using WSGameServer.User.WorkStation;

namespace WSGameServer.User;

public partial class User
{
    /// <summary>
    /// 로그인 시 DB에서 조회한 데이터들을 한 번에 메모리로 적재한다.
    /// 새로운 데이터셋(우편함, 퀘스트 등)이 생기면 인자를 추가한다.
    /// </summary>
    public void LoadDB(
        List<ItemInfo> inventoryItems,
        List<WorkStationSlot> workStationSlots,
        IReadOnlyDictionary<CurrencyType, long> currencies,
        List<Character.Character> characters)
    {
        Inventory.Load(inventoryItems);
        Wallet.Load(currencies);

        // 캐릭터가 슬롯 속도의 근거이므로 슬롯보다 먼저 적재한다.
        LoadCharacters(characters);
        WorkStation.Load(workStationSlots);

        // 배치된 캐릭터의 적성으로 각 슬롯의 속도를 맞춘다.
        // 접속마다 다시 계산하므로 그동안 밸런스가 바뀌었어도 반영된다.
        RefreshWorkStationSpeed(notify: false);
    }
}
