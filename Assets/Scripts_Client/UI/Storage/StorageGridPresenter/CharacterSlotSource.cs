using System.Collections.Generic;
using GameData;

// UnityEngine에도 CharacterInfo(폰트 글리프 정보)가 있어 이름이 겹친다. 우리가 쓰는 건 패킷 쪽이다.
using CharacterInfo = MikaProtocol.CharacterInfo;

// 캐릭터 탭 — 보유 캐릭터를 칸으로 내놓는다.
//
// ⏸ 증분 패킷이 없다 — 캐릭터 목록은 로그인 스냅샷('S_CharacterListResponse') 한 번이 전부다.
//   그런데도 지금 성립하는 이유는 캐릭터가 늘어날 경로 자체가 없기 때문이다(가챠는 아이템 전용).
//   T-026(캐릭터 가챠)이 붙어 'CharactersChanged'가 다시 오기만 하면 이 클래스는 고치지 않아도 된다.
//
// ★ 슬롯 변경도 함께 구독한다 — 보조 문구가 배치 상태라, 배치·해제가 일어나면 이 목록의 내용이 바뀐다.
//   캐릭터 목록만 구독하면 배치를 바꿔도 칸이 "배치 가능"인 채로 남는다.
public class CharacterSlotSource : StorageSlotSource
{
    // 배치 상태 문구. 창고에서 "지금 이 캐릭터를 꺼내 쓸 수 있나"가 판단 근거다.
    private const string AssignedText = "배치 중";
    private const string IdleText     = "배치 가능";

    private readonly PlayerDataModel _data;

    public CharacterSlotSource(PlayerDataModel data)
    {
        _data = data;
    }

    // 보유 캐릭터를 칸으로 옮긴다 (Rebuild에서 호출).
    //
    // ⚠️ 이름은 종류(TID)로 읽는다 — 개체 번호를 'GetCharacterName'에 넣으면 '?#2'가 나온다.
    // ⚠️ 등급은 'None'이다 — 'CharacterTable'에 등급 컬럼이 없다(기획 미정).
    //    등급이 생기면 여기서 그 값을 넘기면 되고 칸은 고치지 않는다.
    protected override void Fill(List<StorageSlotData> into)
    {
        foreach (CharacterInfo character in _data.Characters)
        {
            bool isAssigned = _data.FindSlotIndexOf(character.CharacterId) >= 0;

            into.Add(new StorageSlotData(
                character.CharacterId,
                GameDataLoader.GetCharacterName(character.CharacterTid),
                isAssigned ? AssignedText : IdleText,
                GlobalRarity.None));
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
