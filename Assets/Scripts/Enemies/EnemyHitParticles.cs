using BlackHole.Core;
using PrimeTween;
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

        // 파편 수 = (색 등급+1) × 크기(최대 28). 버퍼는 이 최댓값에 동시 사망 여유를 곱해 둠(넘치면 오래된 파편부터 버려짐).
        private const int MaxDeathFragmentsPerEnemy = 28;
        private const int DeathConcurrencyHeadroom = 16;
        private const float DeathSuctionDelay = 0.12f;
        private const float DeathFragmentSize = 0.65f; // 파편 기준 크기(여러 차례 조정됨). 필요시 에디터에서 추가 조절.
        // 폭발하듯 튀지 않고 죽은 자리 둘레 이 반경 안에 '배치'만 한다(초기 속도 0, DeathSuctionDelay 후 공전 진입).
        private const float FragmentSpreadRadius = 0.5f;
        // Size 1당 파편 크기가 이 비율만큼 커짐(size=4면 1.45배). 반지름 비례 방식 대신 이 방식을 채택함(체감상 더 적합).
        private const float FragmentSizePerEnemySize = 0.15f;
        // 전체 흡입 속도 기준값(Planet 배율 1.0 기준, 기존보다 낮춤).
        private const float SwirlRate = 7.2f;
        private const float InwardRate = 2.5f;
        private const float CenterRadius = 0.06f;
        // 공전 진입을 부드럽게 하는 이징. 전원 동시 진입이라 파편 간 간격과는 무관.
        private const float OrbitEaseInDuration = 0.35f;
        // 당김 시작 이징. ⚠ SuctionStaggerInterval보다 뚜렷하게 짧아야 함 — 안 그러면 여러 파편이 동시에
        // 당겨져 "다 같이 날아가는" 것처럼 보임(한 파편씩 순서대로 보이려면 이 관계 필수).
        private const float InwardEaseInDuration = 0.12f;
        // 공전은 전원 동시 시작, 당김 시작 시점만 파편 순서(i)대로 이 간격씩 늦춤 — 공전 대열 유지한 채
        // 한 파편씩 순서대로 당겨짐. InwardEaseInDuration보다 뚜렷하게 커야 함.
        private const float SuctionStaggerInterval = 0.18f;
        // 당김 시작 후 중심 도달까지의 시간(Planet 기준). 파편마다 수명을 다르게 줘서 늦게 당겨져도 같은
        // 시간을 받게 하고, 실제로는 종류별 속도 배율(SuctionSpeedMultiplierOf)로 나눠 쓴다.
        private const float SuctionTravelDuration = 1.9f;
        // 당김 진행도에 비례해 Ease.InQuad로 가속(완만하게 시작, 끝으로 갈수록 가팔라짐). 스월·당김 두 성분에
        // 같은 배율을 곱해 회전각(궤적 모양)은 유지. 100% 진행 시 속도는 (1+SuctionAccelerationBonus)배.
        private const float SuctionAccelerationBonus = 5f;
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
            // playOnAwake 기본값(true)으로 즉시 재생되므로, main.duration 등을 건드리기 전에 반드시 멈춰야 함
            // (안 그러면 "Setting the duration while system is still playing" 에러).
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

            var deathParticleObject = new GameObject("Enemy Death Particles");
            deathParticleObject.transform.SetParent(transform, false);
            _deathParticles = deathParticleObject.AddComponent<ParticleSystem>();
            _deathParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule deathMain = _deathParticles.main;
            deathMain.playOnAwake = false;
            deathMain.loop = false;
            // 실제 수명은 EmitDeath에서 파편마다 개별 지정됨 — 여기 값은 폴백일 뿐.
            deathMain.duration = DeathSuctionDelay + SuctionTravelDuration;
            deathMain.startLifetime = DeathSuctionDelay + SuctionTravelDuration;
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

        // 적 종류별 흡입 속도 배율(소행성 > 행성 > 별 순으로 빠름). Advance·EmitDeath 양쪽에서 스월·당김
        // 성분에 동일하게 곱해 쓰므로 회전각(궤적 모양)은 유지됨.
        private static float SuctionSpeedMultiplierOf(EnemyType type)
        {
            switch (type)
            {
                case EnemyType.Asteroid:
                    return 1f;
                case EnemyType.Star:
                    return 0.5f;
                case EnemyType.Planet:
                default:
                    return 0.7f;
            }
        }

        // 사망 파편: 폭발 없이 죽은 자리 둘레에 배치만 하고, DeathSuctionDelay 후 공전 진입, Advance가 한
        // 파편씩 순서대로 HQ로 빨아들인다. type은 종류별 흡입 속도 차등에 쓰인다.
        public void EmitDeath(Vector3 position, Color color, EnemyType type, int tier, int size)
        {
            if (!_deathParticles.isPlaying)
                _deathParticles.Play(false);

            // 파편 수 = (Tier+1) × 크기. 예: Tier0·크기2 → 2개, Tier4·크기3 → 15개.
            int fragmentCount = (tier + 1) * size;
            float baseAngle = (_burstIndex++ % 16) * Mathf.PI / 8f;

            float fragmentSizeMultiplier = 1f + (size - 1) * FragmentSizePerEnemySize;
            float speedMultiplier = SuctionSpeedMultiplierOf(type);
            // 종류별 속도에 맞춰 수명 확보(빠른 소행성은 짧게, 느린 별은 길게) — 도중에 사라지지 않도록.
            float travelDuration = SuctionTravelDuration / speedMultiplier;

            for (int i = 0; i < fragmentCount; i++)
            {
                float angle = baseAngle + i * Mathf.PI * 2f / fragmentCount;
                Vector3 spreadOffset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * FragmentSpreadRadius;

                // i번째 파편은 i번째 차례에 당겨짐(스월은 전원 동시 시작). 늦게 당겨지는 만큼 수명도 늘려 모두
                // 동일한 travelDuration을 받게 함.
                float inwardDelay = DeathSuctionDelay + i * SuctionStaggerInterval;
                float startLifetime = inwardDelay + travelDuration;

                var parameters = new ParticleSystem.EmitParams
                {
                    position = position + spreadOffset,
                    velocity = Vector3.zero,
                    startLifetime = startLifetime,
                    startSize = (DeathFragmentSize + (i % 3) * 0.13f) * fragmentSizeMultiplier,
                    startColor = color,
                    rotation = angle + i * 0.61f,
                    // randomSeed 재사용: 하위 16비트=당김 순서, 상위 16비트=EnemyType. 이 시스템은 randomSeed를
                    // 쓰는 다른 모듈이 없어 안전하게 재사용 가능.
                    randomSeed = (uint)i | ((uint)type << 16),
                };

                _deathParticles.Emit(parameters, 1);
            }
        }

        // EnemyView.Synchronize가 매 프레임 호출. 위치 동기화와 같은 타이밍/순서로 돌아야 해서(스크립트
        // 실행 순서에 기대지 않도록) Update() 대신 외부에서 직접 구동.
        public void Advance()
        {
            int count = _deathParticles.GetParticles(_deathParticleBuffer);
            if (count == 0)
                return;

            // 흡입 2단계: 1) 스월(공전) 진입 — 전원 DeathSuctionDelay 시점에 동시 진입(HQ 근처 파편이 연출 위해
            //    멀어지는 걸 막는 핵심). 2) 중심 당김 — randomSeed 하위 16비트(순서)만큼 늦게 당김 성분 추가,
            //    공전은 유지한 채 한 파편씩 차례로 흡수(상위 16비트의 종류별로 속도 차등). 둘 다 Ease.OutQuad로
            //    부드럽게 램프업.
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

                int order = (int)(particle.randomSeed & 0xFFFFu);
                var type = (EnemyType)(particle.randomSeed >> 16);
                float speedMultiplier = SuctionSpeedMultiplierOf(type);

                Vector2 radial = offset / distance;
                Vector2 tangent = new Vector2(-radial.y, radial.x);

                float orbitProgress = Mathf.Clamp01((age - DeathSuctionDelay) / OrbitEaseInDuration);
                float orbitStrength = Easing.Evaluate(orbitProgress, Ease.OutQuad);

                float inwardDelay = DeathSuctionDelay + order * SuctionStaggerInterval;
                float inwardProgress = Mathf.Clamp01((age - inwardDelay) / InwardEaseInDuration);
                float inwardStrength = Easing.Evaluate(inwardProgress, Ease.OutQuad);

                // 당김 진행도에 비례 가속(두 성분에 동일 배율이라 회전각은 유지).
                float travelProgress = Mathf.Clamp01((age - inwardDelay) / (SuctionTravelDuration / speedMultiplier));
                float accelerationMultiplier = 1f + Easing.Evaluate(travelProgress, Ease.InQuad) * SuctionAccelerationBonus;

                Vector2 velocity = (tangent * (distance * SwirlRate * orbitStrength) - radial * (distance * InwardRate * inwardStrength))
                    * speedMultiplier * accelerationMultiplier;
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