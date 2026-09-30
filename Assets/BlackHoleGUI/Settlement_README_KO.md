# 결산표 아이콘 추가팩

기존 GUI와 같은 벡터 도형으로 다시 그린 PNG/SVG입니다. 테두리 원을 제거하여 숫자 옆에 붙이는 작은 통계 아이콘으로 사용할 수 있습니다.

## 구성
- Skills: breaker, piercing_laser. 각 스킬의 누적 피해량 옆에 표시.
- Effects: chain_lightning, explosion. 연쇄 전기·원형 폭발처럼 별도 집계되는 피해 원인용. 신규 스킬이 구현됐다는 의미는 아닙니다.
- Enemies: asteroid, golden_asteroid, electric_asteroid, moon, planet, star, comet, supernova. 종류별 파괴 수 또는 종류별 획득 골드 옆에 표시.
- Metrics: total_damage, destroyed_count, gold, experience, duration, growth_time, critical_hits, hit_count, skill_activations, growth_level.
- GrowthColors: 행성/항성의 6색 버전. 01~06은 시각 색상 순서일 뿐 데이터 레벨/등급 ID와 자동 연결되지 않습니다.
- Components: 밝은/어두운 통계 행 배경, 막대 track 및 흰색 fill.

22종 의미 아이콘 × 4색 버전 + 성장 색상 12개 + 컴포넌트 4개 = PNG 104개 및 대응 SVG 104개.

## 적용
BlackHoleGUI 폴더를 Assets/BlackHoleGUI에 합칩니다. 기존 Editor/BlackHoleGUIImporter.cs가 있으면 그대로 유지하세요. 포함된 동일 스크립트를 다른 위치에 중복 설치하지 마세요.

PNG/Settlement 아래 에셋을 Unity UI Image의 Source Image에 넣습니다. 아이콘은 256×256 투명 RGBA이며 UI에는 32~56px 정도로 표시합니다. Image Type Simple, Preserve Aspect 켜기.

- ink: 밝은 배경용.
- ivory: 첨부한 결산표처럼 어두운 배경용.
- white: Image.color로 원하는 색을 지정하는 마스크.
- color: 구분용 기본 색상을 넣은 버전. Image.color를 흰색으로 유지.

컴포넌트는 512px 폭입니다. 행 배경은 512×128, 막대는 512×48입니다. Sprite Editor에서 Border를 네 방향 모두 16px로 설정하고 Image Type Sliced로 사용하세요. 이 추가팩 컴포넌트의 Border는 기본 임포터가 자동 지정하지 않으므로 수동 설정이 필요합니다. 아이콘에는 Border가 필요 없습니다.

bar_fill은 흰색입니다. Image.color로 스킬 색상을 지정하고 RectTransform의 폭을 실제 피해 비율에 맞춰 변경하거나 프로젝트의 Slider Fill로 사용하세요. 막대는 같은 척도를 사용해야 비교가 가능합니다. 예시 이미지는 가장 큰 피해량을 100% 너비로 삼았습니다.

## 통계 연결 원칙
아이콘과 실제 수치는 분리합니다. 아이콘 오른쪽에는 기존 텍스트 컴포넌트로 수치를 표시하세요. 숫자, 단위, 수치 로직은 이미지에 들어 있지 않습니다.

스킬별 피해량은 Damage Source별 집계를 사용합니다. 연쇄 효과를 스킬 피해에 포함했다면 Effects 행을 추가로 합산하여 중복 계산하지 마세요. 피해 수치가 raw damage인지 실제 적용 피해인지 기존 전투 집계 정책에 맞춰 제목/툴팁을 정하세요.

파괴 수에는 종류별 Kill Count를 연결합니다. 총계는 배타적으로 분류된 행만 합산합니다. 황금 소행성을 소행성 총계의 부분집합으로 표시한다면 소행성+황금을 그대로 더하면 안 됩니다.

첨부된 원작 스크린샷에서 의미를 확인할 수 없는 작은 기호는 임의의 스킬로 복제하지 않았습니다. 현재 알려진 스킬, 피해 효과, 천체 종류를 대상으로 제작했습니다.

## 파일 안내
Settlement_mapping.csv: 의미, 용도, 각 테마별 정확한 경로.
Settlement_manifest.json: 전체 파일과 해상도·Border 정보.
Settlement_PREVIEW.png: 아이콘 목록.
Settlement_LAYOUT_EXAMPLE.png: 결산표 배치 예시. 표시 수치는 모두 예시이며 실제 게임 집계값이 아닙니다. 이 이미지는 UI 전체를 통째로 붙이는 용도가 아닙니다.

PNG 알파, 크기, SVG 렌더링을 확인했습니다. Unity 내 컴파일/실행은 미검증입니다. 프리팹·통계 집계 코드·UI 바인딩은 포함하지 않습니다.
