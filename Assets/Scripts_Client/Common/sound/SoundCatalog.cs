using System;
using System.Collections.Generic;
using UnityEngine;

// 소리 목록 — 키로 소리를 찾는다. 'Resources/SoundCatalog.asset' 하나를 처음 찾을 때 한 번 읽는다.
//
// ■ 왜 씬이 소리를 직접 쥐지 않고 Resources로 찾나
//   소리 파일을 별도 저장소(아트 저장소 등)에 두는 프로젝트가 있다. 씬이 소리를 직접 쥐면 그 저장소가 없는 PC에서
//   참조가 깨진다 — 목록이 없으면 'Of'가 null을 돌려주고 'SoundManager'는 조용히 건너뛴다.
//
// ■ 목록 채우기는 프로젝트 몫이다 — 인스펙터에서 손으로 넣어도 되고, 폴더를 훑어 파일 이름을 키로 쓰는 에디터 도구를 붙여도 된다.
//   키는 코드에 흩지 않고 프로젝트의 이름표 클래스 한 곳에 모은다('XxxSounds.Click => SoundCatalog.Of("click")').
//
// ■ 목록은 있는데 키가 없으면 한 번 경고한다 — 파일 이름을 바꾸거나 키를 잘못 적으면 게임은 조용히 소리 없이 돈다.
[CreateAssetMenu(menuName = "Arca/Sound Catalog", fileName = "SoundCatalog")]
public class SoundCatalog : ScriptableObject
{
    // 'Resources.Load' 경로 — 아무 'Resources/' 폴더 아래 'SoundCatalog.asset'
    public const string ResourcePath = "SoundCatalog";

    [Serializable]
    public struct Entry
    {
        public string    key;
        public AudioClip clip;
    }

    [SerializeField, Tooltip("키 → 소리. 키는 보통 파일 이름이다")]
    private Entry[] sounds = Array.Empty<Entry>();

    private Dictionary<string, AudioClip>? _map;

    private static SoundCatalog? _current;
    private static bool          _searched;
    private static readonly HashSet<string> Warned = new();

    // 도메인 리로드를 끈 에디터에서 지난 플레이의 목록을 들고 있지 않게 비운다 (Unity 런타임 초기화 훅 · 목록을 다시 쓴 에디터 도구도 리플렉션으로 부른다)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        if (_current != null)
        {
            _current._map = null;
        }

        _current  = null;
        _searched = false;
        Warned.Clear();
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

        if (_current._map.TryGetValue(key, out AudioClip clip))
        {
            return clip;
        }

        if (Warned.Add(key))
        {
            Debug.LogWarning($"[SoundCatalog] 목록에 '{key}'가 없다 — 파일 이름과 키가 같은지 본다.", _current);
        }

        return null;
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
