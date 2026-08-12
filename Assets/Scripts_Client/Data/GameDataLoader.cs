using System;
using System.Collections.Generic;
using System.IO;
using GameData;
using UnityEngine;

/// <summary>
/// 엑셀에서 생성된 게임 테이블('GameTable')을 StreamingAssets에서 읽어 적재한다.
/// </summary>
/// <remarks>
/// 데이터 파이프라인 · 'RuntimeInitializeOnLoadMethod'를 쓰는 이유 ·
/// ⚠️ TID와 개체 번호 함정은 'Data 규칙.md' 참조.
/// </remarks>
public static class GameDataLoader
{
    // .bytes 들이 놓이는 StreamingAssets 하위 폴더 (generate-tables.ps1이 여기로 미러링한다)
    private const string DataFolderName = "Data";

    private static bool _isLoaded;

    /// <summary>테이블이 적재됐는지. 실패 시 false로 남아 있다.</summary>
    public static bool IsLoaded => _isLoaded;

    // 씬 로드 전에 테이블을 적재한다 (Unity 런타임 초기화 훅)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void LoadOnStartup()
    {
        Load();
    }

    /// <summary>
    /// 모든 테이블을 적재한다. 이미 적재됐으면 아무것도 하지 않는다.
    /// 파일이 없거나 깨졌으면 예외를 그대로 올린다(fail-fast).
    /// </summary>
    public static void Load()
    {
        if (_isLoaded)
            return;

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

    /// <summary>
    /// 아이템 이름을 조회한다. 표시용이라 예외 없이 '?#Id'로 떨어지고, 처음 한 번만 경고한다.
    /// </summary>
    public static string GetItemName(int itemId)
    {
        if (GameTable.ItemTable.TryGet(itemId, out var row))
            return row.Name;

        WarnUnknownId("아이템", itemId, _warnedItemIds);
        return $"?#{itemId}";
    }

    /// <summary>캐릭터 이름을 조회한다. 규칙은 'GetItemName'과 같다.</summary>
    public static string GetCharacterName(long characterId)
    {
        if (GameTable.CharacterTable.TryGet((int)characterId, out var row))
            return row.Name;

        WarnUnknownId("캐릭터", (int)characterId, _warnedCharacterIds);
        return $"?#{characterId}";
    }

    // 테이블에 없는 Id를 처음 만났을 때만 경고한다 (GetItemName·GetCharacterName에서 호출)
    private static void WarnUnknownId(string kind, int id, HashSet<int> warned)
    {
        if (!warned.Add(id))
            return;

        ClientLogger.Warn(ClientLogger.Data, $"{kind} 테이블에 없는 Id {id}가 들어왔다. " +
                                       $"서버가 보내는 Id와 엑셀 데이터가 어긋났는지 확인할 것.");
    }
}
