using System.Collections.Generic;
using System.Text;

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
// ■ 보조 문구는 적성 요약이다 (T-046)
// 예전에는 '배치 중'·'배치 가능'이었는데, 배치 여부는 마크가 말하게 되면서 한 칸이 같은 말을
// 두 번 하게 됐다. 그 자리를 적성에 넘긴다 — 배치 목록이 적성 0인 캐릭터를 걸러 내므로
// **"낚시를 눌렀더니 내 캐릭터가 없다"의 답이 여기밖에 없다.**
public class CharacterSlotSource : StorageSlotSource
{
    // 어느 산업도 못 다루는 캐릭터의 보조 문구. 빈 줄로 두면 고장과 구분되지 않는다.
    private const string NoAptitudeText = "적성 없음";

    private readonly PlayerDataModel _data;

    // 적성 요약을 짓는 자리. 칸마다 새로 만들지 않으려고 들고 재사용한다.
    private readonly StringBuilder _aptitudeText = new StringBuilder();

    public CharacterSlotSource(PlayerDataModel data)
    {
        _data = data;
    }

    // 보유 캐릭터를 칸으로 옮긴다 (Rebuild에서 호출).
    //
    // ⚠️ 이름도 등급도 종류(TID)로 읽는다 — 개체 번호를 넣으면 이름은 '?#2'가 되고 등급은 회색('None')이 된다.
    //    칸에 실어 보내는 'Key'만 개체 번호('CharacterId')다.
    protected override void Fill(List<StorageSlotData> into)
    {
        foreach (CharacterInfo character in _data.Characters)
        {
            into.Add(new StorageSlotData(
                character.CharacterId,
                GameDataLoader.GetCharacterName(character.CharacterTid),
                BuildAptitudeText(character),
                GameDataLoader.GetCharacterRarity(character.CharacterTid)));
        }
    }

    // 다룰 수 있는 산업만 이어 붙인다 — "농사7·낚시2" (Fill에서 호출).
    //
    // 적성 0인 산업은 뺀다. 5종을 다 적으면 칸 한 줄에 들어가지 않고,
    // 0은 "못 한다"라 알려 줄 값이 아니라 알려 줄 것이 없다는 뜻이다.
    private string BuildAptitudeText(CharacterInfo character)
    {
        _aptitudeText.Clear();

        foreach (var aptitude in character.Aptitudes)
        {
            if (aptitude.Value == 0)
            {
                continue;
            }

            if (_aptitudeText.Length > 0)
            {
                _aptitudeText.Append('·');
            }

            _aptitudeText.Append(IndustryLabel.Get(aptitude.Industry)).Append(aptitude.Value);
        }

        return _aptitudeText.Length > 0 ? _aptitudeText.ToString() : NoAptitudeText;
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
