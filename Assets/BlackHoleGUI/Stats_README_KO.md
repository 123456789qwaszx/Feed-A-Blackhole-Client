# 업그레이드 스탯 노드 추가팩

사용자가 제공한 36개 stat 키 전부를 포함합니다. 각 스탯마다 독립 PNG/SVG가 있으며 이미지에 글자·숫자는 들어가지 않습니다. PNG는 256×256 투명 RGBA입니다.

## 바로 적용
압축 속 BlackHoleGUI 폴더를 프로젝트의 Assets에 넣으세요. 기존 패키지가 있으면 같은 폴더에 합칩니다. PNG/Stats와 SVG/Stats만 추가되며 기존 이미지 이름은 바꾸지 않습니다. Editor/BlackHoleGUIImporter.cs는 기존 것과 동일한 파일입니다. 이미 있으면 기존 스크립트를 유지하고 새 사본을 별도 경로에 두지 마세요.

단독 사용도 가능합니다. 포함된 Editor 스크립트가 PNG를 Sprite / Single / Full Rect / 중앙 피벗 / 무압축으로 설정합니다. 필요하면 Tools > Black Hole GUI > Reimport PNG Sprites를 실행하세요.

- 아이콘: PNG/Stats/Icons/ink/{stat}.png
- 밝은 아이콘: PNG/Stats/Icons/ivory/{stat}.png
- Unity Image.color 착색용: PNG/Stats/Icons/white/{stat}.png
- 완성 노드: PNG/Stats/Nodes/{stat}__{state}.png
- 상태: available, hover, purchased, locked

예: enemy.asteroid.growth-supply__purchased.png

Image Type은 Simple, Preserve Aspect는 켜기. 완성 노드는 Image.color를 흰색으로 유지하세요. 권장 화면 크기는 64~96px이며 작은 모바일 화면에서는 복합 기호 판독성을 확인하세요.

기본=available, Highlighted/Selected=hover를 배정할 수 있습니다. purchased와 locked는 실제 노드 상태에 맞춰 코드에서 Source Image를 교체하세요. Button의 pressed는 순간 눌림이므로 purchased와 같지 않습니다. 이 팩은 노드 영구 상태 4종이며 눌림 애니메이션이나 구매 로직은 포함하지 않습니다.

## 공통 도형 문법
주 기호는 좌상단, 속성 보조 기호는 우하단입니다.

| 구분 | 도형 |
|---|---|
| 소행성 | 육각형 |
| 위성 | 초승달 |
| 행성 | 궤도선이 지난 원 |
| 항성 | 다섯 갈래 별 |
| 혜성 | 원과 평행 꼬리 |
| 전기 소행성 | 육각형 속 번개 |
| 초신성 | 중심점을 가진 여덟 갈래 폭발 |
| 질량 단계 | 높아지는 막대 |
| 시작 공급 | 출발선 + 진입 화살표 + 더하기 |
| 레벨업 공급 | 상승 계단 + 더하기 |
| 등장 확률 | 퍼센트 |
| 황금 비율 | 마름모 + 퍼센트 |
| 황금 보상 배율 | 마름모 + 곱하기 |
| 크기 등급 해금 | 대각선 확장 |
| 상위 천체 변환 | 원본 도형 → 대상 도형 |
| 치명타 확률 | 충격 기호 + 퍼센트 |
| 치명타 배율 | 충격 기호 + 곱하기 |
| 레벨업 추가 시간 | 모래시계 + 상승 계단·더하기 |

enemy.asteroid.upgrade는 소행성→행성, enemy.planet.upgrade는 행성→항성입니다. chance는 대상 천체의 등장 확률을 뜻합니다. 비율 단위(0~1 또는 %)나 실제 적용값은 이미지에 인코딩하지 않았습니다. 수치 툴팁과 계산은 기존 데이터 정의를 따르세요. size-level은 크기 등급 해금이며 모든 개체를 즉시 크게 만드는 의미가 아닙니다.

locked는 내용 대신 자물쇠를 표시합니다. 구매 불가지만 내용을 보여야 한다면 available에 회색 tint를 적용하는 등 별도 상태 처리가 필요합니다.

Stats_mapping.csv: 36개 stat와 파일 경로의 정확한 매핑.
Stats_manifest.json: 전체 252개 PNG/SVG 쌍의 경로·해상도.
Stats_PREVIEW.png: 전체 스탯 노드 목록.

모든 PNG의 크기·알파와 SVG 렌더링을 검사했습니다. Unity 프로젝트 내 컴파일 및 Play Mode 검증은 하지 못했습니다.
