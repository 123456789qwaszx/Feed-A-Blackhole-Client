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

        // 사망 시 파티클. 파편 수는 EmitDeath에서 (색 등급 + 1) × 크기로 계산한다(고정값이 아니다).
        // 버퍼는 이 곱의 큰 값(28)에 동시 사망 여유를 곱해 둔다. 넘치면 파티클 시스템이 오래된 파편부터 버린다.
        private const int MaxDeathFragmentsPerEnemy = 28;
        private const int DeathConcurrencyHeadroom = 16;
        private const float DeathLifetime = 1.8f;
        private const float DeathSuctionDelay = 0.12f;
        private const float DeathFragmentSize = 0.45f; // 안 보인다는 피드백으로 기존 0.24에서 키움. 에디터에서 눈으로 보고 더 조절해도 된다.
        private const float DeathBurstSpeed = 4f;
        private const float SwirlRate = 8.5f;
        private const float InwardRate = 3f;
        private const float CenterRadius = 0.06f;
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
        private ParticleSystem _deathParticles;
        private readonly ParticleSystem.Particle[] _deathParticleBuffer = new ParticleSystem.Particle[MaxDeathFragmentsPerEnemy * DeathConcurrencyHeadroom];
        private Texture2D _fragmentTexture;
        private Material _fragmentMaterial;
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

            // 사망 시 파티클 로직
            var deathParticleObject = new GameObject("Enemy Death Particles");
            deathParticleObject.transform.SetParent(transform, false);
            _deathParticles = deathParticleObject.AddComponent<ParticleSystem>();
            _deathParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule deathMain = _deathParticles.main;
            deathMain.playOnAwake = false;
            deathMain.loop = false;
            deathMain.duration = DeathLifetime;
            deathMain.startLifetime = DeathLifetime;
            deathMain.startSpeed = 0;
            deathMain.startSize = DeathFragmentSize;
            deathMain.startColor = Color.white;
            deathMain.maxParticles = _deathParticleBuffer.Length;
            deathMain.simulationSpace = ParticleSystemSimulationSpace.Local;

            ParticleSystem.EmissionModule deathEmission = _deathParticles.emission;
            deathEmission.rateOverTime = 0;
            deathEmission.rateOverDistance = 0;

            ParticleSystem.ShapeModule deathShape = _deathParticles.shape;
            deathShape.enabled = false;

            ParticleSystem.ColorOverLifetimeModule deathColor = _deathParticles.colorOverLifetime;
            deathColor.enabled = true;
            deathColor.color = fade;

            ParticleSystemRenderer deathRenderer = _deathParticles.GetComponent<ParticleSystemRenderer>();
            deathRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            deathRenderer.sharedMaterial = _fragmentMaterial;
            deathRenderer.sortingOrder = 2;
        }

        public bool IsClear => _particles.particleCount == 0 && _deathParticles.particleCount == 0;

        public void Clear()
        {
            _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _deathParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void OnDestroy()
        {
            Object.Destroy(_fragmentMaterial);
            Object.Destroy(_fragmentTexture);
        }

        // 피격 파편: 맞은 자리에서 작은 파편 몇 개를 사방으로 튀긴다. color는 맞은 적의 색이다.
        public void EmitHit(Vector3 position, Color color)
        {
            if (!_particles.isPlaying)
                _particles.Play(false);

            float baseAngle = (_burstIndex++ % 8) * Mathf.PI / 4f;

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

        // 사망 파편: 죽은 자리에서 크고 빠른 파편을 사방으로 튀긴 뒤, Advance가 HQ 쪽으로 빨아들인다. color는 죽은 적의 색이다.
        public void EmitDeath(Vector3 position, Color color, int tier, int size)
        {
            if (!_deathParticles.isPlaying)
                _deathParticles.Play(false);

            // 파편 수 = 색 등급(Tier는 0부터라 +1) × 크기(1부터).
            // 예: 빨강(Tier 0)·크기 2 → 1×2 = 2개, 파랑(Tier 4)·크기 3 → 5×3 = 15개.
            int fragmentCount = (tier + 1) * size;
            float baseAngle = (_burstIndex++ % 16) * Mathf.PI / 8f;

            for (int i = 0; i < fragmentCount; i++)
            {
                float angle = baseAngle + i * Mathf.PI * 2f / fragmentCount;
                float speed = DeathBurstSpeed + (i % 3) * 0.8f;
                var parameters = new ParticleSystem.EmitParams
                {
                    position = position,
                    velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * speed,
                    startLifetime = DeathLifetime,
                    startSize = DeathFragmentSize + (i % 3) * 0.13f,
                    startColor = color,
                    rotation = angle + i * 0.61f,
                };

                _deathParticles.Emit(parameters, 1);
            }
        }

        // EnemyView.Synchronize에서 매 프레임 한 번 호출된다. 이 컴포넌트는 EnemyView당 하나뿐이라
        // Update()로 두어도 인스턴스 수에 비례해 늘어나는 비용은 아니지만, 흡입 스월 계산이
        // 위치 동기화와 같은 타이밍에 같은 순서로 돌아야 하므로(스크립트 실행 순서에 기대지 않도록)
        // 공용 루프 쪽에서 직접 구동한다.
        public void Advance()
        {
            int count = _deathParticles.GetParticles(_deathParticleBuffer);
            if (count == 0)
                return;

            // 흡입 단계: 잠깐 퍼진 뒤 접선 방향으로 회전시키면서 HQ 원점 쪽으로 끌어당긴다.
            bool changed = false;
            for (int i = 0; i < count; i++)
            {
                ParticleSystem.Particle particle = _deathParticleBuffer[i];
                float age = particle.startLifetime - particle.remainingLifetime;
                if (age < DeathSuctionDelay)
                    continue;

                Vector2 offset = new Vector2(particle.position.x, particle.position.y);
                float distance = offset.magnitude;
                if (distance <= CenterRadius)
                {
                    particle.remainingLifetime = 0;
                    _deathParticleBuffer[i] = particle;
                    changed = true;
                    continue;
                }

                Vector2 radial = offset / distance;
                Vector2 tangent = new Vector2(-radial.y, radial.x);
                Vector2 velocity = tangent * (distance * SwirlRate) - radial * (distance * InwardRate);
                particle.velocity = new Vector3(velocity.x, velocity.y, 0);
                _deathParticleBuffer[i] = particle;
                changed = true;
            }

            if (changed)
                _deathParticles.SetParticles(_deathParticleBuffer, count);
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