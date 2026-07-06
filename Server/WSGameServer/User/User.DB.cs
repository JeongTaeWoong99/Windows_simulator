using MikaProtocol;

namespace WSGameServer.User;

public partial class User
{
    /// <summary>
    /// 로그인 시 DB에서 조회한 데이터들을 한 번에 메모리로 적재한다.
    /// 새로운 데이터셋(우편함, 퀘스트 등)이 생기면 List 인자를 추가한다.
    /// </summary>
    public void LoadDB(List<ItemInfo> inventoryItems)
    {
        Inventory.Load(inventoryItems);
    }
}
