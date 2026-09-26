using System.Runtime.CompilerServices;

namespace WSGameServer;

/// <summary>
/// 테스트 어셈블리가 뜰 때 게임 테이블을 한 번 적재한다. 공용 상수(<c>GameData.Constants</c>)가 테이블을 읽기 때문이다 —
/// 테스트마다 적재를 잊으면 병렬 실행 순서에 따라 붙었다 떨어지는 테스트가 된다.
/// </summary>
internal static class TestAssemblySetup
{
    [ModuleInitializer]
    internal static void LoadGameTables() => GameTableFixture.EnsureLoaded();
}
