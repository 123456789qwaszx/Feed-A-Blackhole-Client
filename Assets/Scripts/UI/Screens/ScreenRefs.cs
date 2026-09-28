using System;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace BlackHole.Unity
{
    // 화면 프리팹이 지켜야 하는 약속: 화면 클래스의 Refs 이름과 같은 이름의 자식이 있어야 한다.
    // UIBase가 자식을 이름으로 찾기 때문이다. 빠진 자식을 에디터와 디버그 빌드에서 경고로 알린다.
    internal static class ScreenRefs
    {
        [Conditional("UNITY_EDITOR"), Conditional("DEBUG")]
        public static void WarnMissing<TRefs>(UIBase view) where TRefs : struct, Enum
        {
            foreach (string name in Enum.GetNames(typeof(TRefs)))
            {
                if (!view.TryGetRect(name, out _))
                    Debug.LogWarning($"[{view.GetType().Name}] 자식 '{name}'이 없다.", view);
            }
        }
    }
}
