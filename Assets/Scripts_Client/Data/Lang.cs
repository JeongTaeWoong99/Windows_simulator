using System;
using System.Collections.Generic;
using GameData;
using UnityEngine;

// 화면 언어 — 'GameData.Loc'의 지금 언어를 정하고, 바뀌면 알린다.
//
// 문구는 전부 번역 테이블('LocalizationTable')에서 온다. 데이터 문구는 'Loc.Get(row.NameLocKey)',
// 클라 고정 문구는 'Lang.Get("UI.<화면>.<이름>")'으로 읽는다. 키 규칙·번역 파일은 'Data 규칙.md' 5절.
//
// ⚠️ 화면은 언어가 바뀌면 'Changed'를 받아 스스로 다시 그린다 — 다시 그리지 않은 글씨는 옛 언어로 남는다.
public static class Lang
{
    // 저장값이 없을 때(첫 실행) — OS 언어가 한국어면 KR, 아니면 EN.
    private const int NoSavedValue = -1;

    // 언어가 바뀌었다. 열린 화면이 받아 문구를 다시 그린다.
    public static event Action? Changed;

    public static Language Current => Loc.Current;

    // 설정 드롭다운의 항목 순서 — 'Language' enum 순서 그대로다.
    public static IReadOnlyList<Language> All { get; } = (Language[])Enum.GetValues(typeof(Language));

    // 도메인 리로드를 끈 에디터에서 지난 플레이의 구독이 남지 않게 비운다 (Unity 런타임 초기화 훅)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Changed = null;
    }

    // 씬의 어떤 'Awake'보다 먼저 저장된 언어를 건다 — 첫 글씨부터 그 언어로 찍힌다 (Unity 런타임 초기화 훅)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void LoadOnStartup()
    {
        int saved = WindowSettings.LoadInt(WindowSettings.LanguageKey, NoSavedValue);

        Loc.Current = Enum.IsDefined(typeof(Language), saved) ? (Language)saved : SystemDefault();
    }

    // 언어를 바꾸고 저장한다. 같은 언어면 아무것도 하지 않는다 (설정 › 일반 드롭다운).
    public static void Set(Language language)
    {
        if (Loc.Current == language)
        {
            return;
        }

        Loc.Current = language;
        WindowSettings.SaveInt(WindowSettings.LanguageKey, (int)language);
        Changed?.Invoke();
    }

    // 지금 언어의 문구. 없는 키는 키가 그대로 나온다 — 화면에서 빠진 번역이 바로 보인다.
    public static string Get(string key)
    {
        return Loc.Get(key);
    }

    // 그 키의 문구가 있는가 — 코드 이름으로 키를 만드는 표(결과 코드 등)가 빠진 줄을 골라낼 때.
    public static bool Has(string key)
    {
        return GameTable.LocalizationTable.TryGet(key, out _);
    }

    // 자리표시자('{0}' …)가 있는 문구를 채운다. 번역문도 같은 번호를 쓴다.
    public static string Format(string key, params object[] args)
    {
        return string.Format(Loc.Get(key), args);
    }

    // 드롭다운에 쓰는 언어 이름 — 그 언어 자신의 표기라 어느 언어로 보고 있어도 찾을 수 있다.
    public static string NativeName(Language language)
    {
        switch (language)
        {
            case Language.KR: return "한국어";
            case Language.EN: return "English";
            default:          return language.ToString();
        }
    }

    private static Language SystemDefault()
    {
        return Application.systemLanguage == SystemLanguage.Korean ? Language.KR : Language.EN;
    }
}
