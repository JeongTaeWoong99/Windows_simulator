using System.Collections.Generic;
using GameData;
using MikaProtocol;

// 장비 탭 — 보유 장비를 칸으로 내놓는다.
//
// ※ 목록은 로그인 스냅샷('S_EquipListResponse')이 원본이고, 지급·장착·해제는 개체 단위 푸시
//   ('S_EquipSyncResponse')로 온다 — 둘 다 'EquipsChanged' 하나로 온다.
//
// ■ 개체가 키다 — 캐릭터 탭과 같은 모양이다
//   같은 장비를 여러 개 가질 수 있어 종류(TID)로는 하나를 특정하지 못한다.
//   칸의 'Key'는 개체 번호('EquipId')이고, 이름·등급·효과는 TID로 테이블에서 읽는다.
//
// ■ 칸 순서의 주인은 서버다
//   'EquipInfo.SlotPosition'이 창고 칸 번호이고 서버가 빈 자리를 찾아 넣는다
//   (서버 'User.NextFreeEquipPosition'). 자원·캐릭터는 아직 서버 위치가 없어 도착 순서로 그리지만
//   (T-058 → T-044), 장비는 이미 있으므로 **그 순서를 따른다** — 재접속해도 자리가 유지된다.
//   [정렬]을 누른 뒤의 자리 기억은 자원·캐릭터와 똑같이 세션 한정이다(기반 클래스 주석).
//
// ■ 장착 중인 장비도 여기 남는다
//   서버가 장착할 때 'SlotPosition'을 비우지 않는다(서버 'Equip.Wear'). 목록에서 빼면
//   "장비가 사라졌다"로 읽히므로 그대로 두고, 격자가 '배' 마크로 구분한다
//   ('StorageGridPresenter.Redraw' — 캐릭터의 배치 마크와 같은 자리다).
public class EquipSlotSource : StorageSlotSource
{
    private readonly PlayerDataModel _data;

    // 'SlotPosition'으로 줄 세울 때 쓰는 버퍼. 매번 새로 만들지 않는다(상주 앱이라 GC가 쌓인다).
    private readonly List<EquipInfo> _ordered = new List<EquipInfo>();

    public EquipSlotSource(PlayerDataModel data)
    {
        _data = data;
    }

    // 보유 장비를 서버 칸 번호 순서로 옮긴다 (Rebuild에서 호출).
    protected override void Fill(List<SlotData> into)
    {
        _ordered.Clear();
        _ordered.AddRange(_data.Equips);
        _ordered.Sort(CompareBySlotPosition);

        foreach (EquipInfo equip in _ordered)
        {
            into.Add(new SlotData(
                equip.EquipId,
                GameDataLoader.GetEquipName(equip.EquipTid),
                BuildEffectText(equip.EquipTid),
                GameDataLoader.GetEquipRarity(equip.EquipTid)));
        }
    }

    // 장비 [정렬] 규칙 — 등급 높은 순 → 부위 → 산업 → 종류(TID) → 개체 번호 (Sort에서 호출).
    //
    // 같은 장비를 여러 개 가질 수 있어 개체 번호까지 가야 동점이 없다.
    // ※ 칸의 'Key'는 개체 번호라 TID는 보유 목록에서 찾아온다.
    protected override int CompareForSort(SlotData a, SlotData b)
    {
        int byRarity = ((byte)b.Rarity).CompareTo((byte)a.Rarity);

        if (byRarity != 0)
        {
            return byRarity;
        }

        int tidA = _data.GetEquipTid(a.Key);
        int tidB = _data.GetEquipTid(b.Key);

        bool hasA = GameDataLoader.TryGetEquip(tidA, out EquipTableRow rowA);
        bool hasB = GameDataLoader.TryGetEquip(tidB, out EquipTableRow rowB);

        // 테이블에 없는 TID는 이름이 '?#'으로 보이는 칸이라 뒤로 민다.
        if (!hasA || !hasB)
        {
            int byKnown = (hasA ? 0 : 1).CompareTo(hasB ? 0 : 1);

            return byKnown != 0 ? byKnown : a.Key.CompareTo(b.Key);
        }

        int byKind = ((byte)rowA.EquipKind).CompareTo((byte)rowB.EquipKind);

        if (byKind != 0)
        {
            return byKind;
        }

        int byIndustry = ((byte)rowA.Industry).CompareTo((byte)rowB.Industry);

        if (byIndustry != 0)
        {
            return byIndustry;
        }

        int byTid = tidA.CompareTo(tidB);

        return byTid != 0 ? byTid : a.Key.CompareTo(b.Key);
    }

    // 장비 변경 구독 (Subscribe에서 호출)
    //
    // ※ 캐릭터 탭과 달리 이벤트 하나만 듣는다 — 지급도 장착·해제도 전부
    //   'S_EquipSyncResponse'로 와서 'EquipsChanged' 하나로 합쳐지기 때문이다.
    protected override void OnSubscribe()
    {
        _data.EquipsChanged += Rebuild;
    }

    // 구독 해제 (Unsubscribe에서 호출)
    protected override void OnUnsubscribe()
    {
        _data.EquipsChanged -= Rebuild;
    }

    // 칸의 보조 문구 — '낚시 +30%' · 전 산업이면 '전산업 +10%' (Fill에서 호출).
    //
    // ⚠️ 산업 'None'에 'IndustryLabel'을 쓰지 않는다 — 거기서는 '미지정'이 나오는데,
    //   장비의 None은 지정을 안 한 것이 아니라 **어느 산업에나 붙는다**는 뜻이다.
    // ※ 'SpeedAddPermille'은 천분율이다(100 = 10%). 감산 장비가 생길 수 있어 부호를 함께 만든다.
    private static string BuildEffectText(int equipTid)
    {
        if (!GameDataLoader.TryGetEquip(equipTid, out EquipTableRow row))
        {
            return "";
        }

        string industry = row.Industry == IndustryType.None
            ? "전산업"
            : IndustryLabel.Get((EIndustryType)(byte)row.Industry);

        string sign = row.SpeedAddPermille >= 0 ? "+" : "";

        return $"{industry} {sign}{row.SpeedAddPermille / 10f:0.#}%";
    }

    // 서버가 정한 창고 칸 번호 순서 (Fill의 정렬 비교자).
    // ⚠️ 끝까지 가서 0이 나오지 않게 개체 번호까지 본다 — 'List.Sort'는 안정 정렬이 아니다.
    private static int CompareBySlotPosition(EquipInfo a, EquipInfo b)
    {
        int byPosition = a.SlotPosition.CompareTo(b.SlotPosition);

        return byPosition != 0 ? byPosition : a.EquipId.CompareTo(b.EquipId);
    }
}
