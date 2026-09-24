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
// ■ 장착 중인 장비는 여기 없다 (2026-09-25 · T-086)
//   캐릭터가 끼고 있으면 **창고 목록에서 빠진다** — 팰월드식으로, 장착은 "캐릭터에게 옮긴 것"이다.
//   보유 전량은 「창고 + 장착 중」의 합집합이고, 장착분은 작업슬롯 세팅 화면에서 보인다.
//
//   ⚠️ **서버는 아직 장착분을 창고 칸에서 빼지 않는다** — 'Equip.Wear'가 'SlotPosition'을
//   비우지 않고, 'User.StorageCapacity' 검사도 장착 여부를 가리지 않는다.
//   그래서 **"빈 칸이 보이는데 뽑기가 거절되는" 구간이 남아 있다.** 서버 쪽 제외가 T-086의 몫이고,
//   그때까지의 안내 문구는 T-064가 갖는다. 화면만 먼저 바뀐 상태라는 것을 알고 봐야 한다.
//
// ※ 칸의 보조 문구('낚시 +30%')는 'UI/Shared/EquipLabel'이 만든다 —
//   작업슬롯 세팅의 장비 칸이 같은 문구를 쓰기 때문이다(T-074).
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
            // 장착 중인 장비는 창고에 없다 — 캐릭터에게 옮겨 간 것으로 본다(클래스 주석).
            if (_data.IsEquipped(equip.EquipId))
            {
                continue;
            }

            into.Add(new SlotData(
                equip.EquipId,
                GameDataLoader.GetEquipName(equip.EquipTid),
                EquipLabel.GetEffectText(equip.EquipTid),
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

    // 서버가 정한 창고 칸 번호 순서 (Fill의 정렬 비교자).
    // ⚠️ 끝까지 가서 0이 나오지 않게 개체 번호까지 본다 — 'List.Sort'는 안정 정렬이 아니다.
    private static int CompareBySlotPosition(EquipInfo a, EquipInfo b)
    {
        int byPosition = a.SlotPosition.CompareTo(b.SlotPosition);

        return byPosition != 0 ? byPosition : a.EquipId.CompareTo(b.EquipId);
    }
}
