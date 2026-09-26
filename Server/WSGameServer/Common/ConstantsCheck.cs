using System.Reflection;
using GameData;

namespace WSGameServer;

/// <summary>
/// 공용 상수(<see cref="Constants"/>)를 기동 때 전부 한 번 읽어 본다. 코드와 <c>.bytes</c>가 다른 판이면
/// (행을 지운 뒤 한쪽만 배포) 여기서 멈춘다 — 그 값을 처음 쓰는 순간까지 숨어 있지 않게.
/// </summary>
public static class ConstantsCheck
{
    /// <returns>읽은 상수 수.</returns>
    public static int EnsureAll()
    {
        var properties = typeof(Constants).GetProperties(BindingFlags.Public | BindingFlags.Static);
        foreach (var property in properties)
        {
            try
            {
                property.GetValue(null);
            }
            catch (TargetInvocationException e)
            {
                throw new InvalidDataException($"Constants.{property.Name}을 ConstantsTable에서 읽지 못했다 — 코드와 데이터 판이 다르다", e.InnerException);
            }
        }

        return properties.Length;
    }
}
