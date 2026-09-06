using System;
using System.Collections.Generic;
using System.IO;
using GameData;
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
    // ※ 창고 캐릭터 목록이 쓰는 값이다. 가챠 결과창은 이 함수를 쓰지 않는다 —
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
