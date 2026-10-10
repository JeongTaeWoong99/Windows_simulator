using System;
using System.Collections.Generic;
using System.IO;
using GameData;
using MikaProtocol; // EIndustryType — 화면이 쓰는 산업 축. 테이블의 GameData.IndustryType과 값이 같다
using UnityEngine;

// 엑셀에서 생성된 게임 테이블('GameTable')을 StreamingAssets에서 읽어 적재한다.
//
// 데이터 파이프라인 · 'RuntimeInitializeOnLoadMethod'를 쓰는 이유 ·
// ⚠️ TID와 개체 번호 함정은 'Data 규칙.md' 참조.
public static class GameDataLoader
{
    // .bytes 들이 놓이는 StreamingAssets 하위 폴더 (generate-tables.ps1이 여기로 미러링한다)
    private const string DataFolderName = "Data";

    private static bool _isLoaded;

    // 테이블이 적재됐는지. 실패 시 false로 남아 있다.
    public static bool IsLoaded => _isLoaded;

    // 씬 로드 전에 테이블을 적재한다 (Unity 런타임 초기화 훅)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void LoadOnStartup()
    {
        Load();
    }

    // 모든 테이블을 적재한다. 이미 적재됐으면 아무것도 하지 않는다.
    // 파일이 없거나 깨졌으면 예외를 그대로 올린다(fail-fast).
    public static void Load()
    {
        if (_isLoaded)
        {
            return;
        }

        string dataPath = Path.Combine(Application.streamingAssetsPath, DataFolderName);

        try
        {
            GameTable.LoadAll(fileName => File.ReadAllBytes(Path.Combine(dataPath, fileName)));
        }
        catch (Exception e)
        {
            // 예외는 그대로 올린다(fail-fast). 다만 원인 지점을 먼저 말해 준다 —
            // 스택만 보면 "파일이 없다"까지는 알아도 어느 폴더를 봐야 하는지, 무엇이 그 폴더를
            // 채우는지가 안 나온다.
            ClientLogger.Error(ClientLogger.Data,
                $"테이블 적재 실패 — {dataPath}\n" +
                $"    StreamingAssets에 .bytes가 없거나 깨졌다. GameDesign/generate-tables.ps1을 실행해 생성물을 갱신할 것.\n" +
                $"    {e.GetType().Name}: {e.Message}");

            throw;
        }

        _isLoaded = true;
        ClientLogger.Info(ClientLogger.Data, $"테이블 적재 완료 — 아이템 {GameTable.ItemTable.Count}종, 캐릭터 {GameTable.CharacterTable.Count}종");
    }

    // 이미 경고한 Id. 매 프레임 갱신되는 UI에서 같은 경고가 쏟아지는 것을 막는다.
    private static readonly HashSet<int> _warnedItemIds      = new HashSet<int>();
    private static readonly HashSet<int> _warnedCharacterIds = new HashSet<int>();
    private static readonly HashSet<int> _warnedEquipTids    = new HashSet<int>();
    private static readonly HashSet<int> _warnedAptitudes    = new HashSet<int>();

    // 아이템 이름을 조회한다. 표시용이라 예외 없이 '?#Id'로 떨어지고, 처음 한 번만 경고한다.
    public static string GetItemName(int itemId)
    {
        if (GameTable.ItemTable.TryGet(itemId, out var row))
        {
            return row.Name;
        }

        WarnUnknownId("아이템", itemId, _warnedItemIds);

        return $"?#{itemId}";
    }

    // 아이템 등급을 조회한다. 규칙은 'GetItemName'과 같다 — 없는 Id는 'None'으로 떨어지고 처음 한 번만 경고한다.
    //
    // ※ 가챠 보상은 이 함수를 쓰지 않는다 — 패킷('GachaRewardInfo.Rarity')이 등급을 실어 오므로
    //   그 값을 그대로 쓴다. 테이블을 다시 뒤지면 두 값이 어긋났을 때 조용히 패킷 쪽을 무시하게 된다.
    public static GlobalRarity GetItemRarity(int itemId)
    {
        if (GameTable.ItemTable.TryGet(itemId, out var row))
        {
            return row.GlobalRarity;
        }

        WarnUnknownId("아이템", itemId, _warnedItemIds);

        return GlobalRarity.None;
    }

    // 아이템 분류(산업 · 기타 · 특수)를 조회한다. 규칙은 'GetItemName'과 같다 — 없는 Id는 'None'으로 떨어지고 처음 한 번만 경고한다.
    //
    // ※ 인벤토리 찾기의 산업 판정이 쓰는 값이다('ResourceSlotSource.MatchesIndustry'). 정렬은 서버가 한다(T-044).
    public static ItemType GetItemType(int itemId)
    {
        if (GameTable.ItemTable.TryGet(itemId, out var row))
        {
            return row.ItemType;
        }

        WarnUnknownId("아이템", itemId, _warnedItemIds);

        return ItemType.None;
    }

    // 아이템 판매가를 조회한다. 규칙은 'GetItemName'과 같다 — 없는 Id는 0으로 떨어지고 처음 한 번만 경고한다.
    //
    // ※ 판매 합계 미리보기가 쓰는 값이다. 실제로 얼마를 받을지는 서버가 정하며
    //   ('S_ItemSellResponse.GainedGold'), 지금은 판매율이 100%라 둘이 같다.
    public static int GetItemPrice(int itemId)
    {
        if (GameTable.ItemTable.TryGet(itemId, out var row))
        {
            return row.BasePrice;
        }

        WarnUnknownId("아이템", itemId, _warnedItemIds);

        return 0;
    }

    // 이 아이템이 **열 수 있는 상자**인가. 서버와 같은 판정이다 — 'OpenGachaId'가 있으면 상자다.
    //
    // 상자만 따로 표시하는 분류('ItemType')가 없다 — 열었을 때 무엇이 나오는지가 상자의 정의라
    // 서버도 이 컬럼 하나로 가른다. 여기서 분류를 하나 더 만들면 두 곳이 어긋날 수 있다.
    // ※ 없는 Id는 '상자가 아니다'로 떨어진다 — 경고는 이름·등급 조회에서 이미 나므로 여기서 또 내지 않는다.
    public static bool IsBox(int itemId)
    {
        return GameTable.ItemTable.TryGet(itemId, out var row) && row.OpenGachaId != 0;
    }

    // 이 아이템이 **인챈트 큐브**인가 — 'EnchantItemTable'에 행이 있으면 큐브다. 큐브 창의 버튼 목록('EnchantCubes')과 같은 출처다.
    // ※ 없는 Id는 '큐브가 아니다'로 떨어진다 — 'IsBox'와 같은 이유로 경고를 내지 않는다.
    public static bool IsCube(int itemId)
    {
        return GameTable.EnchantItemTable.TryGet(itemId, out _);
    }

    // 가챠 풀의 메타(이름·비용)를 조회한다. 'GachaId'는 'GachaInfoTID'와 같은 값이다.
    //
    // ※ 여기만 이름·등급 조회와 달리 실패를 그대로 돌려준다 — 값이 없으면 대체할 표시가 없고,
    //   버튼을 만들지 말지를 부르는 쪽이 정해야 하기 때문이다('GachaPresenter'가 배선 오류로 알린다).
    public static bool TryGetGachaInfo(int gachaId, out GachaInfoTableRow row)
    {
        return GameTable.GachaInfoTable.TryGet(gachaId, out row);
    }

    // 캐릭터 이름을 조회한다. 규칙은 'GetItemName'과 같다.
    public static string GetCharacterName(long characterId)
    {
        if (GameTable.CharacterTable.TryGet((int)characterId, out var row))
        {
            return row.Name;
        }

        WarnUnknownId("캐릭터", (int)characterId, _warnedCharacterIds);

        return $"?#{characterId}";
    }

    // 캐릭터 등급을 조회한다. 규칙은 'GetItemRarity'와 같다 — 없는 Id는 'None'으로 떨어지고 처음 한 번만 경고한다.
    //
    // ※ 인벤토리 캐릭터 목록이 쓰는 값이다. 가챠 결과창은 이 함수를 쓰지 않는다 —
    //   패킷('GachaRewardInfo.Rarity')이 등급을 실어 오므로 그 값을 그대로 쓴다('GetItemRarity'와 같은 이유).
    // ⚠️ 종류(TID)를 넣는다 — 개체 번호('CharacterInfo.CharacterId')를 넣으면 조회가 빗나가 'None'이 나온다.
    public static GlobalRarity GetCharacterRarity(int characterTid)
    {
        if (GameTable.CharacterTable.TryGet(characterTid, out var row))
        {
            return row.GlobalRarity;
        }

        WarnUnknownId("캐릭터", characterTid, _warnedCharacterIds);

        return GlobalRarity.None;
    }

    // 캐릭터 기준가('CharacterTable.BasePrice') — 즉시 판매가·경매 등록가의 바닥. 없는 TID면 0.
    public static int GetCharacterPrice(int characterTid)
    {
        if (GameTable.CharacterTable.TryGet(characterTid, out var row))
        {
            return row.BasePrice;
        }

        WarnUnknownId("캐릭터", characterTid, _warnedCharacterIds);

        return 0;
    }

    // 장비 이름을 조회한다. 규칙은 'GetItemName'과 같다.
    // ⚠️ 종류(TID)를 넣는다 — 개체 번호('EquipInfo.EquipId')를 넣으면 조회가 빗나간다.
    public static string GetEquipName(int equipTid)
    {
        if (GameTable.EquipTable.TryGet(equipTid, out var row))
        {
            return row.Name;
        }

        WarnUnknownId("장비", equipTid, _warnedEquipTids);

        return $"?#{equipTid}";
    }

    // 장비 등급을 조회한다. 규칙은 'GetItemRarity'와 같다 — 없는 TID는 'None'으로 떨어지고 처음 한 번만 경고한다.
    public static GlobalRarity GetEquipRarity(int equipTid)
    {
        if (GameTable.EquipTable.TryGet(equipTid, out var row))
        {
            return row.GlobalRarity;
        }

        WarnUnknownId("장비", equipTid, _warnedEquipTids);

        return GlobalRarity.None;
    }

    // 장비 정의 한 행. 부위·산업·속도 가산을 한꺼번에 봐야 하는 쪽이 쓴다(인벤토리 장비 칸의 효과 문구·정렬).
    //
    // ※ 이름·등급과 달리 실패를 그대로 돌려준다 — 대체할 표시가 없고, 여러 값을 동시에 읽는 자리라
    //   빗나갔을 때 무엇으로 떨어뜨릴지를 부르는 쪽이 정해야 한다('TryGetGachaInfo'와 같은 이유).
    public static bool TryGetEquip(int equipTid, out EquipTableRow row)
    {
        return GameTable.EquipTable.TryGet(equipTid, out row);
    }

    // 장비 능력치 옵션 한 행 — 등급·종류·산업·수치. 장비 칸의 능력치 칸 색과 툴팁 문구가 쓴다.
    // ※ 실패를 그대로 돌려준다 — 모르는 옵션은 칸을 비운 것으로 그릴지 부르는 쪽이 정한다('TryGetEquip'과 같은 이유).
    public static bool TryGetEnchantOption(int optionTid, out EnchantOptionTableRow row)
    {
        return GameTable.EnchantOptionTable.TryGet(optionTid, out row);
    }

    // 능력치 옵션 전체('EnchantOptionTable') — 큐브 자동의 예상 확률이 등급별 가중치를 읽는다.
    public static IReadOnlyList<EnchantOptionTableRow> EnchantOptions => GameTable.EnchantOptionTable.All;

    // 이 등급의 장비가 갖는 능력치 칸 수('EnchantGradeTable.SlotCount'). 모르는 등급('None' 포함)이면 0.
    public static int GetEnchantSlotCount(GlobalRarity rarity)
    {
        return GameTable.EnchantGradeTable.TryGet(rarity, out EnchantGradeTableRow row) ? row.SlotCount : 0;
    }

    // 큐브 아이템 목록('EnchantItemTable') — 큐브 창의 버튼이 이 순서로 놓인다.
    public static IReadOnlyList<EnchantItemTableRow> EnchantCubes => GameTable.EnchantItemTable.All;

    // 이 인챈트 등급에서 그 큐브로 한 단계 오를 확률(만분율). 최고 등급·모르는 큐브면 0.
    //   enchantGrade : 장비의 지금 인챈트 등급 ('None'이면 아직 인챈트가 없다 — 첫 사용은 판정 없이 일반이 된다)
    //
    // ⚠️ 서버 'EnchantCatalog.RankUpPermyriadOf'의 사본이다 — 기본 확률(만분율) × 큐브 배율(천분율) ÷ 1000, **정수 내림**.
    //   화면에 보여 주기만 하고 판정은 서버가 한다. 식이 바뀌면 둘을 함께 고친다.
    // ※ 'EnchantGradeTable'의 행은 두 뜻을 함께 싣는다 — 'SlotCount'는 **장비 등급**으로, 'UpPermyriad'는 **인챈트 등급**으로 읽는다.
    public static int GetEnchantUpPermyriad(GlobalRarity enchantGrade, int cubeTid)
    {
        if (enchantGrade >= GlobalRarity.Mythic
            || !GameTable.EnchantGradeTable.TryGet(enchantGrade, out EnchantGradeTableRow grade)
            || !GameTable.EnchantItemTable.TryGet(cubeTid, out EnchantItemTableRow cube))
        {
            return 0;
        }

        return (int)((long)grade.UpPermyriad * cube.UpRatePermille / 1000);
    }

    // 우편 템플릿 한 행 — 제목·본문·발신자는 패킷에 없고 여기서만 읽는다.
    // ※ 첨부는 이 행이 아니라 패킷('MailInfo')의 것을 쓴다 — 서버가 보낸 순간 복사해 두므로
    //   템플릿을 나중에 고쳐도 이미 온 우편은 그대로이고, 넘침 보관 템플릿은 첨부가 비어 있다.
    // ※ 실패를 그대로 돌려준다 — 제목·발신자를 한꺼번에 대체해야 해서 부르는 쪽이 정한다('TryGetEquip'과 같은 이유).
    public static bool TryGetMailTemplate(int templateTid, out MailTemplateTableRow row)
    {
        return GameTable.MailTemplateTable.TryGet(templateTid, out row);
    }

    // 적성(0~10)의 기본 작업속도(천분율, 1000 = 1.0배). 없는 적성이면 0이고 처음 한 번만 경고한다.
    //
    // ※ 캐릭터 적성과 달리 클라가 테이블을 읽어도 된다 — 적성 → 속도는 **개체마다 갈라지지 않는 정적 곡선**이다.
    //   서버도 같은 표를 쓴다('Character.GetBaseWorkSpeed'). 적성 값 자체는 서버가 준 것('PlayerDataModel.GetAptitude')을 넣는다.
    public static int GetBaseWorkSpeed(byte aptitude)
    {
        if (GameTable.WorkSpeedTable.TryGet(aptitude, out var row))
        {
            return row.BaseWorkSpeedPermille;
        }

        WarnUnknownId("작업속도", aptitude, _warnedAptitudes);

        return 0;
    }

    // 레벨 L에 **도달하는 데** 직전 레벨에서 필요한 경험치를 조회한다. 행이 없으면 false.
    //
    // ※ 경고하지 않는다 — 행이 없는 것은 오류가 아니라 **만렙**이라는 뜻이다(마지막 행 = 만렙).
    //   그래서 'Level + 1'로 물어 false면 만렙으로 읽는다. 진행 바의 분모도 'Level + 1'의 값이다.
    // ※ 적성 → 속도처럼 **개체마다 갈라지지 않는 정적 곡선**이라 클라가 읽어도 된다.
    //   레벨·경험치 값 자체는 서버가 준 것('CharacterInfo.Level'·'Exp')을 쓴다.
    public static bool TryGetRequiredExp(int level, out int requiredExp)
    {
        if (GameTable.CharacterLevelTable.TryGet(level, out var row))
        {
            requiredExp = row.RequiredExp;

            return true;
        }

        requiredExp = 0;

        return false;
    }

    // 응축 최고 ★ — 'CharacterStarTable'의 행 수(★1부터 빠짐없이 이어진다). 행이 없으면 0이고 응축할 수 없다.
    public static int CondenseMaxStar => GameTable.CharacterStarTable.All.Count;

    // 그 ★에 닿는 **누적** 재료 수 (응축 창의 진행 바). ★0 이하는 0, 최고 ★을 넘으면 최고 ★의 값.
    //
    // ⚠️ 서버 'CharacterStarCatalog'의 사본이다 — 표의 'RequiredCount'는 **그 단계의 몫**이라 앞 단계까지 더한다.
    // ※ 값은 화면에만 쓴다. ★ 자체는 서버가 'CharacterInfo.Star'로 준다.
    public static int GetCondenseThreshold(int star)
    {
        int total = 0;

        for (int i = 1; i <= Mathf.Min(star, CondenseMaxStar); i++)
        {
            if (GameTable.CharacterStarTable.TryGet(i, out CharacterStarTableRow row))
            {
                total += row.RequiredCount;
            }
        }

        return total;
    }

    // 누적 재료 수로 닿는 ★ (응축 창의 "넣으면 몇 ★"). 서버 'CharacterStarCatalog.StarAt'의 사본이다.
    public static int GetCondenseStarAt(int condenseCount)
    {
        int star = 0;

        while (star < CondenseMaxStar && condenseCount >= GetCondenseThreshold(star + 1))
        {
            star++;
        }

        return star;
    }

    // 이 ★의 작업속도 가산(천분율, **총량**). ★0·행 없음은 0이다. 서버 'CharacterStarCatalog.SpeedAddAt'의 사본이다.
    public static int GetStarSpeedAdd(int star)
    {
        if (star <= 0)
        {
            return 0;
        }

        return GameTable.CharacterStarTable.TryGet(Mathf.Min(star, CondenseMaxStar), out CharacterStarTableRow row)
            ? row.SpeedAddPermille
            : 0;
    }

    // 작업슬롯 한 칸을 여는 해금 TID. 'WorkSlotTable'에 없는 칸이거나 조건 없는 칸이면 0.
    //
    // ※ 해금 조건은 클라가 테이블에서 만든다 — 서버는 열린 목록만 준다(기획 unlock 1장 #9).
    public static int GetWorkSlotUnlockTid(int slotIndex)
    {
        return GameTable.WorkSlotTable.TryGet(slotIndex, out var row) ? row.UnlockTID : 0;
    }

    // 해금 한 줄(골드·계정 레벨·선행)을 조회한다. 0이거나 없는 TID면 false.
    public static bool TryGetUnlock(int unlockTid, out UnlockTableRow row)
    {
        return GameTable.UnlockTable.TryGet(unlockTid, out row);
    }

    // 계정 레벨 L에 **도달하는 데** 직전 레벨에서 필요한 경험치. 행이 없으면 false(= 만렙).
    //
    // ※ 캐릭터용 'TryGetRequiredExp'와 **다른 함수다** — 곡선이 캐릭터의 8배라 값이 long이고,
    //   읽는 테이블도 'AccountLevelTable'로 다르다. 하나로 합치면 형이 맞지 않는다.
    public static bool TryGetAccountRequiredExp(int level, out long requiredExp)
    {
        if (GameTable.AccountLevelTable.TryGet(level, out var row))
        {
            requiredExp = row.RequiredExp;

            return true;
        }

        requiredExp = 0;

        return false;
    }

    // 특성 전체 (엑셀 순서 그대로). 특성 화면이 산업·효과로 갈라 쓴다.
    //
    // ※ **TID 규칙을 클라에 베끼지 않는다.** 무엇을 하는 특성인지는 'EffectType'으로,
    //   어느 산업인지는 'Industry'로 갈린다 — 시트에 특성이 늘면 화면도 저절로 따라온다.
    public static IReadOnlyList<UserTraitTableRow> UserTraits => GameTable.UserTraitTable.All;

    // 특성 한 줄(이름·레벨당 비용·레벨당 효과·기본/최대 레벨)을 조회한다. 없는 TID면 false.
    //
    // ※ 레벨마다 다른 것은 계정 레벨 조건뿐이다 — 그건 'TryGetUserTraitLevel'이 준다.
    public static bool TryGetUserTrait(int userTraitTid, out UserTraitTableRow row)
    {
        return GameTable.UserTraitTable.TryGet(userTraitTid, out row);
    }

    // 특성을 'level'로 올릴 때의 조건 행(계정 레벨)을 조회한다. 그 레벨이 없으면 false.
    //
    // ※ 시트 키는 'UserTraitLevelTID'(= 특성 × 100 + 레벨)지만 그 계산식을 여기 베끼지 않는다 —
    //   (특성, 레벨)로 한 번 뒤집어 둔다('TryGetIndustryLevel'과 같은 이유).
    public static bool TryGetUserTraitLevel(int userTraitTid, int level, out UserTraitLevelTableRow row)
    {
        if (_userTraitLevels == null)
        {
            _userTraitLevels = new Dictionary<(int, int), UserTraitLevelTableRow>();

            foreach (var candidate in GameTable.UserTraitLevelTable.All)
            {
                _userTraitLevels[(candidate.UserTraitTID, candidate.Level)] = candidate;
            }
        }

        return _userTraitLevels.TryGetValue((userTraitTid, level), out row!);
    }

    // (특성, 레벨) → 레벨 조건 행. 처음 물을 때 한 번 만든다 (테이블은 적재 후 불변이다).
    private static Dictionary<(int Trait, int Level), UserTraitLevelTableRow>? _userTraitLevels;

    // 이 산업의 개척 특성('IndustryUnlock')을 찾는다. 없으면 false.
    //
    // ■ 산업 레벨은 해금 행이 아니라 이 특성의 레벨로 열린다 (2026-10-02 · T-108)
    // **특성 레벨 N = 그 산업 Lv N까지 열림** — 판정은 'PlayerDataModel.IsIndustryLevelOpen'.
    public static bool TryGetIndustryUnlockTrait(EIndustryType industry, out UserTraitTableRow row)
    {
        foreach (var candidate in GameTable.UserTraitTable.All)
        {
            if (candidate.EffectType == UserTraitEffect.IndustryUnlock && (byte)candidate.Industry == (byte)industry)
            {
                row = candidate;

                return true;
            }
        }

        row = null!;

        return false;
    }

    // 산업 레벨 한 줄(이름·요구 점수·판정당 경험치)을 (산업, 레벨)로 조회한다. 없으면 false.
    //
    // ※ 시트 키는 'IndustryLevelTID'지만 화면이 묻는 축은 (산업, 레벨)이라 여기서 한 번 뒤집는다
    //   (서버 'IndustryLevelCatalog'와 같은 모양). 키 계산식을 화면마다 적지 않기 위해서다.
    public static bool TryGetIndustryLevel(EIndustryType industry, int level, out IndustryLevelTableRow row)
    {
        return IndustryLevels.TryGetValue(((byte)industry, level), out row!);
    }

    // 이 산업에 존재하는 가장 높은 레벨 (버튼을 몇 개 그릴지). 행이 하나도 없으면 0.
    public static int GetMaxIndustryLevel(EIndustryType industry)
    {
        int max = 0;

        foreach (var row in GameTable.IndustryLevelTable.All)
        {
            if ((byte)row.IndustryType == (byte)industry && row.Level > max)
            {
                max = row.Level;
            }
        }

        return max;
    }

    // (산업, 레벨) → 산업 레벨 행. 처음 물을 때 한 번 만든다 (테이블은 적재 후 불변이다).
    private static Dictionary<(byte Industry, int Level), IndustryLevelTableRow>? _industryLevels;

    private static Dictionary<(byte Industry, int Level), IndustryLevelTableRow> IndustryLevels
    {
        get
        {
            if (_industryLevels != null)
            {
                return _industryLevels;
            }

            _industryLevels = new Dictionary<(byte, int), IndustryLevelTableRow>();

            foreach (var row in GameTable.IndustryLevelTable.All)
            {
                _industryLevels[((byte)row.IndustryType, row.Level)] = row;
            }

            return _industryLevels;
        }
    }

    // 이 산업 레벨에서 나오는 자원 목록. 없는 (산업, 레벨)이면 빈 목록.
    //
    // ■ 산업마다 테이블이 따로다
    // 'FarmingBasicTable' … 'HuntingBasicTable' 다섯이 **모양은 같고 타입만 다르다.**
    // 화면이 산업으로 분기하면 그 분기가 화면마다 복사되므로 **여기 한 곳에만 둔다.**
    //
    // ※ 비율은 화면이 만든다 — 'Weight' 합으로 나누면 된다(서버 'WeightedPicker'와 같은 축).
    public static IReadOnlyList<IndustryDrop> GetIndustryDrops(EIndustryType industry, int level)
    {
        var key = ((byte)industry, level);

        if (IndustryDrops.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var list = new List<IndustryDrop>();

        switch (industry)
        {
            case EIndustryType.Farming:
                foreach (var row in GameTable.FarmingBasicTable.All)
                {
                    if (row.IndustryLevel == level) { list.Add(new IndustryDrop(row.ItemTID, row.Weight)); }
                }
                break;

            case EIndustryType.Fishing:
                foreach (var row in GameTable.FishingBasicTable.All)
                {
                    if (row.IndustryLevel == level) { list.Add(new IndustryDrop(row.ItemTID, row.Weight)); }
                }
                break;

            case EIndustryType.Mining:
                foreach (var row in GameTable.MiningBasicTable.All)
                {
                    if (row.IndustryLevel == level) { list.Add(new IndustryDrop(row.ItemTID, row.Weight)); }
                }
                break;

            case EIndustryType.Logging:
                foreach (var row in GameTable.LoggingBasicTable.All)
                {
                    if (row.IndustryLevel == level) { list.Add(new IndustryDrop(row.ItemTID, row.Weight)); }
                }
                break;

            case EIndustryType.Hunting:
                foreach (var row in GameTable.HuntingBasicTable.All)
                {
                    if (row.IndustryLevel == level) { list.Add(new IndustryDrop(row.ItemTID, row.Weight)); }
                }
                break;
        }

        var drops = list.ToArray();
        IndustryDrops[key] = drops;

        return drops;
    }

    // (산업, 레벨) → 자원 목록. 처음 물을 때 한 번 만든다 (테이블은 적재 후 불변이다).
    private static readonly Dictionary<(byte Industry, int Level), IndustryDrop[]> IndustryDrops
        = new Dictionary<(byte Industry, int Level), IndustryDrop[]>();

    // 이 산업 레벨의 판정마다 따로 굴리는 상자 목록 (이슈 #49). 없는 (산업, 레벨)이면 빈 목록.
    //
    // ■ 두 시트에서 고른다 — 서버 'CommonRewardCatalog.Roll'과 같은 규칙이다
    // 기본은 'CommonRewardTable'의 **레벨 행**(전 산업 공통)이고, 'CommonRewardOverrideTable'에
    // 그 (산업, 레벨) 행이 하나라도 있으면 공통 행을 **통째로** 대신한다(섞지 않는다).
    public static IReadOnlyList<IndustryBoxReward> GetIndustryBoxRewards(EIndustryType industry, int level)
    {
        var key = ((byte)industry, level);

        if (IndustryBoxRewards.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var list = new List<IndustryBoxReward>();

        foreach (var row in GameTable.CommonRewardOverrideTable.All)
        {
            if ((byte)row.IndustryType == (byte)industry && row.IndustryLevel == level)
            {
                list.Add(new IndustryBoxReward(row.ItemTID, row.Count, row.ChancePerMillion));
            }
        }

        if (list.Count == 0)
        {
            foreach (var row in GameTable.CommonRewardTable.All)
            {
                if (row.IndustryLevel == level)
                {
                    list.Add(new IndustryBoxReward(row.ItemTID, row.Count, row.ChancePerMillion));
                }
            }
        }

        var rewards = list.ToArray();
        IndustryBoxRewards[key] = rewards;

        return rewards;
    }

    // (산업, 레벨) → 상자 목록. 처음 물을 때 한 번 만든다 (테이블은 적재 후 불변이다).
    private static readonly Dictionary<(byte Industry, int Level), IndustryBoxReward[]> IndustryBoxRewards
        = new Dictionary<(byte Industry, int Level), IndustryBoxReward[]>();

    // 테이블에 없는 Id를 처음 만났을 때만 경고한다 (이름·등급·가격 조회에서 호출)
    private static void WarnUnknownId(string kind, int id, HashSet<int> warned)
    {
        if (!warned.Add(id))
        {
            return;
        }

        ClientLogger.Warn(ClientLogger.Data, $"{kind} 테이블에 없는 Id {id}가 들어왔다. " +
                                       $"서버가 보내는 Id와 엑셀 데이터가 어긋났는지 확인할 것.");
    }
}

// 산업 레벨 하나에서 나오는 자원 한 줄 — 아이템 종류와 가중치.
//
// ※ 산업별 드롭 테이블 다섯이 모양은 같고 타입만 달라, 화면이 한 타입으로 읽도록 여기서 합친다
//   ('GameDataLoader.GetIndustryDrops'). 비율은 'Weight' 합으로 나눠 화면이 만든다.
public readonly struct IndustryDrop
{
    public IndustryDrop(int itemTid, int weight)
    {
        ItemTid = itemTid;
        Weight  = weight;
    }

    public int ItemTid { get; }

    public int Weight { get; }
}

// 산업 레벨 하나에서 판정마다 따로 굴리는 상자 한 줄 — 아이템 · 개수 · 백만분율 확률.
//
// ※ 'IndustryDrop'과 달리 **가중치가 아니라 독립 확률**이다 — 줄마다 따로 굴리므로 자원 가중치의
//   분모에 섞지 않는다. 비율은 'ChancePerMillion / 1,000,000'이다('GameDataLoader.GetIndustryBoxRewards').
public readonly struct IndustryBoxReward
{
    public IndustryBoxReward(int itemTid, int count, int chancePerMillion)
    {
        ItemTid          = itemTid;
        Count            = count;
        ChancePerMillion = chancePerMillion;
    }

    public int ItemTid { get; }

    public int Count { get; }

    public int ChancePerMillion { get; }
}
