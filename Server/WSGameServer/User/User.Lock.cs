using MikaProtocol;

namespace WSGameServer;

public partial class User
{
    // 캐릭터·장비 잠금 켜기/끄기(T-137). 이미 같은 상태여도 Ok로 답한다 — 클라가 두 번 눌러도 어긋나지 않게.
    // 잠금은 없애는 동작(판매·경매 등록·응축 재료)만 막는다.
    public void TrySetLock(ELockTarget target, long id, bool locked)
    {
        var code = target switch
        {
            ELockTarget.Character => SetCharacterLock(id, locked),
            ELockTarget.Equip     => SetEquipLock(id, locked),
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
