using UnityEngine;

namespace BlackHole.Net.Unity
{
    public sealed class UnityJsonSerializer : IJsonSerializer
    {
        public T FromJson<T>(string json) where T : class
        {
            return JsonUtility.FromJson<T>(json);
        }

        public string ToJson(object value)
        {
            return JsonUtility.ToJson(value);
        }
    }
}
