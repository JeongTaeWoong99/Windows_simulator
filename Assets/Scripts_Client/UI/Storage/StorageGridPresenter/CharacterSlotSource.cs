using System.Collections.Generic;

// UnityEngine에도 CharacterInfo(폰트 글리프 정보)가 있어 이름이 겹친다. 우리가 쓰는 건 패킷 쪽이다.
using CharacterInfo = MikaProtocol.CharacterInfo;

// 캐릭터 탭 — 보유 캐릭터를 칸으로 내놓는다.
//
// ⏸ 증분 패킷이 없다 — 캐릭터 목록은 로그인 스냅샷('S_CharacterListResponse') 한 번이 전부다.
//   그런데도 지금 성립하는 이유는 캐릭터가 늘어날 경로 자체가 없기 때문이다(가챠는 아이템 전용).
//   T-026(캐릭터 가챠)이 붙어 'CharactersChanged'가 다시 오기만 하면 이 클래스는 고치지 않아도 된다.
//
// ★ 슬롯 변경도 함께 구독한다 — 배치·해제가 일어나면 칸의 '배' 마크가 바뀐다.
//   캐릭터 목록만 구독하면 배치를 바꿔도 마크가 낡은 채로 남는다.
//
// ■ 보조 문구를 쓰지 않는다 — 그 자리는 적성 스트립이 쓴다 (T-048)
// 배치 여부는 '배' 마크가 말하고(T-046), 적성은 하단 5칸 스트립이 말한다.
// 한때 이 자리에서 '농사7·낚시2' 같은 문구를 지었는데, 5종을 다 가진 캐릭터는 19자가 되어
// **100px 칸에서 글자가 5px까지 줄었다.** 문구를 짓는 쪽을 남겨 두면 다음에 보는 사람이
// 어느 표시가 진짜인지 알 수 없으므로 함께 걷어냈다.
//
// ※ 적성은 격자가 읽어 칸에 넘긴다('StorageGridPresenter.Redraw') — '배' 마크와 같은 방식이다.
//   배치 목록이 적성 0인 캐릭터를 걸러 내므로 **"낚시를 눌렀더니 내 캐릭터가 없다"의 답이
//   창고 칸밖에 없다** — 그래서 표시 자체는 빠질 수 없다.
public class CharacterSlotSource : StorageSlotSource
{
    private readonly PlayerDataModel _data;

    public CharacterSlotSource(PlayerDataModel data)
    {
        _data = data;
    }

    // 보유 캐릭터를 칸으로 옮긴다 (Rebuild에서 호출).
    //
    // ⚠️ 이름도 등급도 종류(TID)로 읽는다 — 개체 번호를 넣으면 이름은 '?#2'가 되고 등급은 회색('None')이 된다.
    //    칸에 실어 보내는 'Key'만 개체 번호('CharacterId')다.
    // ※ 보조 문구는 빈 문자열이다 — 그 자리를 적성 스트립이 쓴다(위 주석).
    protected override void Fill(List<SlotData> into)
    {
        foreach (CharacterInfo character in _data.Characters)
        {
            into.Add(new SlotData(
                character.CharacterId,
                GameDataLoader.GetCharacterName(character.CharacterTid),
                "",
                GameDataLoader.GetCharacterRarity(character.CharacterTid)));
        }
    }

    // 캐릭터 목록·슬롯 변경 구독 (Subscribe에서 호출)
    protected override void OnSubscribe()
    {
        _data.CharactersChanged       += Rebuild;
        _data.WorkStationSlotsChanged += Rebuild;
    }

    // 구독 해제 (Unsubscribe에서 호출)
    protected override void OnUnsubscribe()
    {
        _data.CharactersChanged       -= Rebuild;
        _data.WorkStationSlotsChanged -= Rebuild;
    }
}
