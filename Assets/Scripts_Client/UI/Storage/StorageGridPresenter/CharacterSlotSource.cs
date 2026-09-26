using System.Collections.Generic;
using MikaProtocol;

// UnityEngine에도 CharacterInfo(폰트 글리프 정보)가 있어 이름이 겹친다. 우리가 쓰는 건 패킷 쪽이다.
using CharacterInfo = MikaProtocol.CharacterInfo;

// 캐릭터 탭 — 보유 캐릭터를 칸으로 내놓는다.
//
// ※ 목록은 로그인 스냅샷('S_CharacterListResponse')이 원본이고, 레벨·경험치는 정산마다
//   개체 1건씩 푸시('S_CharacterSyncResponse')로 교체된다 — 둘 다 'CharactersChanged' 하나로 온다(T-052).
//
// ■ 레벨은 이름 줄에 넣지 않는다 — 아이콘 왼쪽 위 배지가 말한다
//   한때 'LV.19 폭스파스크'로 이름 줄에 붙였더니 100px 칸에서 글자가 크게 줄었다(2026-09-16).
//   레벨 배지·경험치 게이지는 적성 스트립처럼 격자가 읽어 칸에 넘긴다('StorageGridPresenter.Redraw').
//
// ■ 배치 중인 캐릭터도 **제자리에 남는다** (2026-09-25 결정)
//   작업슬롯에 배치해도 창고에서 빠지지 않는다 — 딤 처리와 '배' 마크로 구분하고, [정렬]에서만 맨 뒤로 민다
//   ('IsAway' · 'Storage 규칙.md'의 "배치 중인 개체도 제자리에 남는다").
//   한때 팰월드식으로 **목록에서 빼는** 안을 넣었다가 되돌렸다 — 창고가 보유 전량의 단일 목록이라는 성질이
//   깨지고, 그 대가로 칸 한도를 넘는 초과분을 따로 관리해야 했다(경위는 로그 '2026-09-24-storage-sort-assigned-first').
//
// ★ 슬롯 변경도 함께 구독한다 — 배치·해제로 딤과 마크가 바뀐다.
//   캐릭터 목록만 구독하면 배치를 바꿔도 칸이 낡은 채로 남는다.
//
// ■ 보조 문구를 쓰지 않는다 — 그 자리는 적성 스트립이 쓴다 (T-048)
// 배치 여부는 '배' 마크가 말하고(T-046), 적성은 하단 5칸 스트립이 말한다.
// 한때 이 자리에서 '농사7·낚시2' 같은 문구를 지었는데, 5종을 다 가진 캐릭터는 19자가 되어
// **100px 칸에서 글자가 5px까지 줄었다.** 문구를 짓는 쪽을 남겨 두면 다음에 보는 사람이
// 어느 표시가 진짜인지 알 수 없으므로 함께 걷어냈다.
//
// ※ 적성은 격자가 읽어 칸에 넘긴다('StorageGridPresenter.Redraw').
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

    // 캐릭터 [정렬] 규칙 — 등급 높은 순 → 종류(TID) 순 → 개체 번호 순 (Sort에서 호출).
    //
    // 같은 종류를 여러 마리 가질 수 있어 개체 번호까지 가야 동점이 없다.
    // ※ 칸의 'Key'는 개체 번호라 TID는 보유 목록에서 찾아온다.
    protected override int CompareForSort(SlotData a, SlotData b)
    {
        int byRarity = ((byte)b.Rarity).CompareTo((byte)a.Rarity);

        if (byRarity != 0)
        {
            return byRarity;
        }

        int byTid = _data.GetCharacterTid(a.Key).CompareTo(_data.GetCharacterTid(b.Key));

        if (byTid != 0)
        {
            return byTid;
        }

        return a.Key.CompareTo(b.Key);
    }

    // 이 캐릭터가 지금 작업슬롯에 나가 있나 (딤·'배' 마크·[정렬] 맨 뒤 — 기반 클래스가 호출).
    //
    // 판정은 'FindSlotIndexOf' 하나로 읽는다 — 작업슬롯 화면도 같은 것을 보므로,
    // 각자 훑으면 두 화면이 "누가 배치 중인지"를 다르게 말한다.
    public override bool IsAway(long key) => _data.FindSlotIndexOf(key) >= 0;

    // 캐릭터 칸 툴팁 — 등급 · 레벨 · 어디서 일하나 · 적성 5종(산업 이름 + 기본 속도) (격자가 칸에 올린 순간 호출).
    //
    // ★ 적성을 **산업 이름과 함께** 적는 것이 본론이다 — 칸의 스트립은 숫자만이라 어느 산업인지는
    //   위치로만 안다(T-048). 기본 속도를 곁들이면 "어디로 보낼까"를 여기서 정할 수 있다.
    // ※ 속도는 적성만의 값이다 — 장비·특성 가산은 빠진다. 실제 속도는 작업슬롯 화면이 말한다.
    public override TooltipContent? BuildTooltip(long key)
    {
        int tid = _data.GetCharacterTid(key);

        if (tid == 0)
        {
            return null; // 목록이 아직 낡았다 — 뒤이어 올 CharactersChanged가 이 칸을 지운다
        }

        var content = new TooltipContent(GameDataLoader.GetCharacterName(tid));

        AddRarityRow(content, GameDataLoader.GetCharacterRarity(tid))
            .Row("레벨", GetLevelLabel(_data.GetCharacterLevel(key)), $"경험치 {_data.GetExpProgress(key):0%}", null)
            .Row("상태", GetWorkText(key))
            .Header("적성");

        foreach (EIndustryType industry in StorageGridPresenter.StripIndustries)
        {
            byte aptitude = _data.GetAptitude(key, industry);
            string speed  = aptitude == 0 ? "" : $"{GameDataLoader.GetBaseWorkSpeed(aptitude) / 1000f:0.00}배";

            content.Row(IndustryLabel.Get(industry), AptitudeLabel.GetText(aptitude), speed, null);
        }

        return content;
    }

    // 레벨 문구 — 'LV.19', 만렙이면 'LV.MAX' (칸 배지 · 툴팁이 호출).
    //
    // ※ 칸 배지와 툴팁이 같은 문구여야 한다 — 한쪽에만 만렙 표기가 있으면 같은 캐릭터를 다르게 말한다.
    // 만렙 = 곡선 테이블에 다음 레벨 행이 없다('GameDataLoader.TryGetRequiredExp').
    public static string GetLevelLabel(int level)
        => GameDataLoader.TryGetRequiredExp(level + 1, out _) ? $"LV.{level}" : "LV.MAX";

    // 어디서 일하는지 — '슬롯 2 · 낚시 Lv.3', 배치돼 있지 않으면 '대기' (BuildTooltip에서 호출).
    //
    // 칸의 '배' 마크는 여부만 말한다 — 여러 슬롯을 돌리면 누가 어디 있는지는 여기서만 알 수 있다.
    private string GetWorkText(long characterId)
    {
        int slotIndex = _data.FindSlotIndexOf(characterId);

        if (slotIndex < 0)
        {
            return "대기";
        }

        foreach (WorkStationSlotInfo slot in _data.WorkStationSlots)
        {
            if (slot.SlotIndex == slotIndex)
            {
                return $"슬롯 {slotIndex} · {IndustryLabel.Get(slot.Industry)} Lv.{slot.IndustryLevel}";
            }
        }

        return $"슬롯 {slotIndex}";
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
