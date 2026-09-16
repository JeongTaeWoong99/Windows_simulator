using GameData;
using MikaProtocol;

namespace WSGameServer;

public partial class User
{
    /// <summary>지급이 끝나면 불린다(로직 스레드). 메모리 적재·싱크는 다음 단계에서 채운다.</summary>
    public void OnEquipGranted(long equipId, int equipTid, int slotPosition)
    {
    }
}
