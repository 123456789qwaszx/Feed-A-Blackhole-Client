using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BlackHole.Unity
{
    internal sealed class EnemyHitParticles : MonoBehaviour
    {
        private const int FragmentCount = 4;
        private const float Lifetime = 0.24f;
        private const float FragmentSize = 0.12f;
        private const float BaseSpeed = 1.5f;
        private const int FragmentTextureSize = 16;
        private static readonly Vector2[] FragmentVertices =
        {
            new Vector2(-0.46f, 0.24f),
            new Vector2(-0.16f, 0.47f),
            new Vector2(0.44f, 0.28f),
            new Vector2(0.29f, -0.18f),
            new Vector2(-0.08f, -0.45f),
            new Vector2(-0.42f, -0.22f),
        };

        private ParticleSystem _particles;
        private Texture2D _fragmentTexture;
        private Material _fragmentMaterial;
        private readonly Dictionary<EnemyId, Color> _colors = new Dictionary<EnemyId, Color>();
        private int _burstIndex;

        private void Awake()
        {
            _particles = gameObject.AddComponent<ParticleSystem>();
            // AddComponent 직후 playOnAwake 기본값(true)으로 바로 재생을 시작해버려서,
            // 아래에서 main.duration 등을 건드리기 전에 반드시 멈춰야 한다(안 그러면
            // "Setting the duration while system is still playing is not supported" 에러가 난다).
            _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = _particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = Lifetime;
            main.startLifetime = Lifetime;
            main.startSpeed = 0;
            main.startSize = FragmentSize;
            main.startColor = Color.white;
            main.maxParticles = FragmentCount * 8;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            ParticleSystem.EmissionModule emission = _particles.emission;
            emission.rateOverTime = 0;
            emission.rateOverDistance = 0;

            ParticleSystem.ShapeModule shape = _particles.shape;
            shape.enabled = false;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = _particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            colorOverLifetime.color = fade;

            _fragmentTexture = CreateFragmentTexture();
            _fragmentMaterial = new Material(Shader.Find("Sprites/Default"))
            {
                name = "Enemy Hit Fragment",
                mainTexture = _fragmentTexture,
            };

            ParticleSystemRenderer particleRenderer = _particles.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.sharedMaterial = _fragmentMaterial;
            particleRenderer.sortingOrder = 2;
        }

        public void Register(Enemy enemy, Color color)
        {
            _colors[enemy.Id] = color;
            enemy.Damaged += Emit;
        }

        public void Unregister(Enemy enemy)
        {
            enemy.Damaged -= Emit;
            _colors.Remove(enemy.Id);
        }

        public void Clear() => _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        private void OnDestroy()
        {
            Object.Destroy(_fragmentMaterial);
            Object.Destroy(_fragmentTexture);
        }

        private void Emit(Enemy enemy)
        {
            if (!_particles.isPlaying)
                _particles.Play(false);

            float baseAngle = (_burstIndex++ % 8) * Mathf.PI / 4f;
            var position = new Vector3(enemy.Position.X, enemy.Position.Y, 0);
            Color color = _colors.TryGetValue(enemy.Id, out Color enemyColor) ? enemyColor : Color.white;

            for (int i = 0; i < FragmentCount; i++)
            {
                float angle = baseAngle + i * Mathf.PI * 2f / FragmentCount;
                float speed = BaseSpeed + (i % 2) * 0.5f;
                var parameters = new ParticleSystem.EmitParams
                {
                    position = position,
                    velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * speed,
                    startLifetime = Lifetime,
                    startSize = FragmentSize * (i % 2 == 0 ? 1f : 0.75f),
                    startColor = color,
                    rotation = angle + i * 0.73f,
                };

                _particles.Emit(parameters, 1);
            }
        }

        private static Texture2D CreateFragmentTexture()
        {
            var texture = new Texture2D(FragmentTextureSize, FragmentTextureSize, TextureFormat.RGBA32, false)
            {
                name = "Enemy Hit Fragment Shape",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color32[FragmentTextureSize * FragmentTextureSize];
            for (int y = 0; y < FragmentTextureSize; y++)
            {
                for (int x = 0; x < FragmentTextureSize; x++)
                {
                    float pointX = (x + 0.5f) / FragmentTextureSize * 2f - 1f;
                    float pointY = (y + 0.5f) / FragmentTextureSize * 2f - 1f;
                    bool inside = false;

                    for (int i = 0, j = FragmentVertices.Length - 1; i < FragmentVertices.Length; j = i++)
                    {
                        Vector2 start = FragmentVertices[j];
                        Vector2 end = FragmentVertices[i];
                        if ((start.y > pointY) != (end.y > pointY) && pointX < (end.x - start.x) * (pointY - start.y) / (end.y - start.y) + start.x)
                            inside = !inside;
                    }

                    pixels[y * FragmentTextureSize + x] = new Color32(255, 255, 255, inside ? (byte)255 : (byte)0);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }
}