using System;
using System.Collections.Generic;
using UnityEngine;

// 소리 목록 — 파일 이름(키)으로 소리를 찾는다. 'Assets/Art/Resources/SoundCatalog.asset' 하나이고,
// 메뉴 '아트/목록만 다시 쓰기'('ArtCatalogSync')가 'Assets/Art/audio/**'를 훑어 채운다.
//
// ■ 그림 목록('VisualCatalog')과 같은 방식이다
//   소리 파일은 그림 저장소(비공개)에 있다. 씬이 소리를 직접 쥐면 그 저장소가 없는 PC에서 참조가 깨진다 —
//   Resources로 찾고, 없으면 조용히 소리 없이 돈다.
//
// ■ 키는 'Sounds'에 모은다 — 파일 이름이 곧 키('ui_click.wav' → "ui_click")
public class SoundCatalog : ScriptableObject
{
    // 'Resources.Load' 경로 — 'Assets/Art/Resources/SoundCatalog.asset'
    public const string ResourcePath = "SoundCatalog";

    [Serializable]
    public struct Entry
    {
        public string    key;
        public AudioClip clip;
    }

    [SerializeField, Tooltip("키(파일 이름) → 소리. 목록 동기화가 채운다 — 손으로 고치지 않는다")]
    private Entry[] sounds = Array.Empty<Entry>();

    private Dictionary<string, AudioClip>? _map;

    private static SoundCatalog? _current;
    private static bool          _searched;

    // 도메인 리로드를 끈 에디터에서 지난 플레이의 목록을 들고 있지 않게 비운다 (Unity 런타임 초기화 훅)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _current  = null;
        _searched = false;
    }

    // 키로 소리를 찾는다. 목록이 없거나(그림 저장소 없음) 키가 없으면 null
    public static AudioClip? Of(string key)
    {
        if (!_searched)
        {
            _searched = true;
            _current  = Resources.Load<SoundCatalog>(ResourcePath);
        }

        if (_current == null)
        {
            return null;
        }

        _current._map ??= ToMap(_current.sounds);

        return _current._map.TryGetValue(key, out AudioClip clip) ? clip : null;
    }

    private static Dictionary<string, AudioClip> ToMap(Entry[] entries)
    {
        var map = new Dictionary<string, AudioClip>();

        foreach (Entry entry in entries)
        {
            if (entry.clip != null)
            {
                map[entry.key] = entry.clip;
            }
        }

        return map;
    }
}
