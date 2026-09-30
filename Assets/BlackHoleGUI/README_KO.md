# Black Hole Geometric GUI Kit v1

앞서 만든 시안을 기반으로 직접 다시 그린 독립 SVG/PNG 에셋입니다. 시안에서 잘라낸 이미지가 아닙니다. 질감·그라데이션 없이 일정한 선과 투명 배경을 사용합니다. Mini Metro의 공식 에셋을 포함하지 않습니다.

## Unity에 적용
1. 압축을 풀고 **BlackHoleGUI 폴더 전체**를 프로젝트의 Assets 폴더 안에 넣습니다.
2. 포함된 Editor/BlackHoleGUIImporter.cs가 PNG를 Sprite (2D and UI), Single, Full Rect, 중앙 피벗, 밉맵 없음, 무압축으로 설정합니다. 컴파일 후 설정이 반영되지 않았으면 Tools > Black Hole GUI > Reimport PNG Sprites를 실행합니다.
3. Canvas의 UI Image > Source Image에 PNG를 넣습니다. 기본 아이콘·노드는 256×256이며 화면상 32~96 크기로 사용하는 것을 권장합니다. Image의 Preserve Aspect를 켭니다.
4. 완성 버튼은 PNG/Buttons/Ready에서 고릅니다. Button Transition을 Sprite Swap으로 설정하고 normal=기본, hover=Highlighted 및 Selected, pressed=Pressed, disabled=Disabled에 배정합니다. Target Graphic은 그 Image입니다.
5. 가변 폭 버튼은 PNG/Buttons/rect_*를 사용하고 Image Type을 Sliced로 설정합니다. panel, tooltip도 Sliced를 사용합니다. 테두리 값은 자동 지정되며 manifest.json에도 들어 있습니다. 원·육각형 버튼과 노드는 Simple + Preserve Aspect를 사용합니다.
6. SVG는 수정용 원본입니다. Unity에는 PNG만으로 적용할 수 있으며 SVG 패키지를 설치할 필요가 없습니다. Editor 스크립트를 원하지 않으면 제거하고 위 임포트 설정을 수동으로 적용합니다.

## 구성
- Icons/ink: 밝은 배경용 먹색 아이콘. Icons/ivory: 어두운 배경용. Icons/white: Unity Image.color로 착색하는 흰색 마스크.
- Nodes: 14종 × 4상태. available=구매 가능, hover=선택, purchased=구매 완료, locked=잠금. 잠긴 노드는 내용 대신 자물쇠를 표시합니다.
- Buttons: 원/사각/육각/가변폭 사각 배경과 7종 완성 버튼. 각 4상태.
- Controls: 패널, 툴팁, 슬라이더, 토글, 체크박스, radial_fill.
- Connections: 직선과 꺾인 연결선의 available/purchased/locked 이미지. 예시 연결선이므로 노드 사이 거리·각도가 바뀌는 실제 트리에서는 UI 선 그리기 구현에 같은 굵기·색을 적용하세요.
- Tokens: 6색 소행성·행성 및 검은 블랙홀. GUI 범례나 작은 표시에 사용 가능합니다.
- Animation: purchase_00~11, 중앙 고정 12프레임. 약 30fps로 한 번 재생하면 0.4초 확산 효과. 구매 상태 변경 뒤 노드 뒤쪽 별도 Image에서 표시합니다. 재생 로직은 프로젝트에서 연결해야 합니다.
- PREVIEW.png: 에셋 미리보기. 실제 파일에는 이름·배경이 없습니다.
- manifest.json: 개별 경로, 해상도, 9-slice 경계.

## 형태·색·움직임 규칙
- 검은 원=블랙홀, 육각형=소행성, 원/고리=행성, 마름모=골드.
- 녹색 채움=완료, 외곽선=구매 가능, 회색+자물쇠=잠금. 색만으로 구분하지 않습니다.
- 메뉴 아이콘은 기능을 표현하고 노드의 테두리와 채움은 상태를 표현합니다.
- 시간 HUD: Controls/radial_fill을 Image Type Filled / Radial360으로 사용하고 fillAmount를 남은 시간 비율로 연결할 수 있습니다. 이 이미지는 꽉 찬 원 마스크입니다. 고리 모양이 필요하면 위에 배경색의 작은 원을 겹쳐 중앙을 가립니다. 숫자는 별도 텍스트로 표시합니다.
- 흰색 아이콘에 Image.color로 원하는 색을 지정할 수 있습니다. 이미 색이 있는 완성 노드·버튼은 Image.color를 흰색으로 유지합니다.
- hover는 1.05배, pressed는 0.94배, 구매는 한 번의 확산을 권장합니다. 이는 동작 제안이며 버튼 전환 애니메이션 코드는 포함하지 않았습니다.

## 확인 범위
모든 PNG의 투명 알파·크기·빈 이미지 여부와 SVG 렌더링을 검사했습니다. Unity 에디터 실행 환경이 없어 프로젝트 내 컴파일·Play Mode 검증은 하지 못했습니다. 이 패키지는 이미지와 임포트 보조 스크립트이며 게임 상태/UI 이벤트 바인딩 및 완성 화면 프리팹은 포함하지 않습니다.
