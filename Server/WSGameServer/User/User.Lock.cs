using GameData;
using MikaProtocol;

namespace WSGameServer;

public partial class User
{
    // 캐릭터·장비·자원·아티팩트 잠금 켜기/끄기(T-137 · T-159 · T-188). 이미 같은 상태여도 Ok로 답한다 — 클라가 두 번 눌러도 어긋나지 않게.
    // 잠금은 없애는 동작(판매·경매 등록·응축 재료)만 막는다.
    public void TrySetLock(ELockTarget target, long id, bool locked)
    {
        var code = target switch
        {
            ELockTarget.Character => SetCharacterLock(id, locked),
            ELockTarget.Equip     => SetEquipLock(id, locked),
            ELockTarget.Resource  => SetItemLock(id, locked),
            ELockTarget.Artifact  => SetArtifactLock(id, locked),
            _                     => EResultCode.InvalidLockTarget,
        };

        if (code != EResultCode.Ok)
        {
            ServerLog.Warn("잠금", $"거절 {code} Uid={Uid} {target} {id} → {locked}");
            Send(new S_EntityLockResponse { Result = code, Target = target, Id = id });
            return;
        }

        PostDBTask(new EntityLockRepository(this, target, id, locked));
        Send(new S_EntityLockResponse { Result = EResultCode.Ok, Target = target, Id = id, Locked = locked });
    }

    // 자원은 개체가 없어 종류(TID)로 잠근다(T-159). 보유가 0개가 돼도 지우지 않는다.
    private readonly HashSet<int> _lockedItemTids = new();

    public bool IsItemLocked(int itemTid) => _lockedItemTids.Contains(itemTid);

    public void LoadItemLocks(IEnumerable<UserItemLockRow> rows)
    {
        _lockedItemTids.Clear();
        foreach (var row in rows)
        {
            _lockedItemTids.Add(row.item_id);
        }
    }

    private EResultCode SetItemLock(long itemTid, bool locked)
    {
        if (itemTid is <= 0 or > int.MaxValue || !GameTable.ItemTable.TryGet((int)itemTid, out _))
        {
            return EResultCode.InvalidLockTarget;
        }

        if (locked)
        {
            _lockedItemTids.Add((int)itemTid);
        }
        else
        {
            _lockedItemTids.Remove((int)itemTid);
        }

        return EResultCode.Ok;
    }

    // 아티팩트도 개체가 없어 번호로 잠근다(T-188). 자원 잠금과 번호 공간이 달라 따로 둔다.
    private readonly HashSet<int> _lockedArtifactTids = new();

    public bool IsArtifactLocked(int artifactTid) => _lockedArtifactTids.Contains(artifactTid);

    public void LoadArtifactLocks(IEnumerable<UserArtifactLockRow> rows)
    {
        _lockedArtifactTids.Clear();
        foreach (var row in rows)
        {
            _lockedArtifactTids.Add(row.artifact_tid);
        }
    }

    private EResultCode SetArtifactLock(long artifactTid, bool locked)
    {
        if (artifactTid is <= 0 or > int.MaxValue || !GameTable.ArtifactTable.TryGet((int)artifactTid, out _))
        {
            return EResultCode.InvalidLockTarget;
        }

        if (locked)
        {
            _lockedArtifactTids.Add((int)artifactTid);
        }
        else
        {
            _lockedArtifactTids.Remove((int)artifactTid);
        }

        return EResultCode.Ok;
    }

    private EResultCode SetCharacterLock(long characterId, bool locked)
    {
        if (!TryGetCharacter(characterId, out var character))
        {
            return EResultCode.CharacterNotOwned;
        }

        character.IsLocked = locked;
        return EResultCode.Ok;
    }

    private EResultCode SetEquipLock(long equipId, bool locked)
    {
        if (!TryGetEquip(equipId, out var equip))
        {
            return EResultCode.EquipNotOwned;
        }

        equip.IsLocked = locked;
        return EResultCode.Ok;
    }
}
