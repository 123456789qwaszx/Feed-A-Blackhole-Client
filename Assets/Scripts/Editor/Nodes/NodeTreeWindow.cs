using System;
using System.Collections.Generic;
using System.Globalization;
using BlackHole.Authoring;
using BlackHole.Core;
using BlackHole.Unity;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace BlackHole.EditorTools
{
    // 노드 도구(F03, 메뉴 BlackHole > Node Tree). 노드 목록 에셋(NodeCatalog)을 격자 위에서 고친다.
    //
    // - 편집: 선은 그은 것만이다. 놓기·옮기기는 선을 건드리지 않는다(좌표는 표시용, 선은 게임 규칙).
    //   잇기는 Shift+끌기, 끊기는 선을 눌러 Delete. 여러 노드를 고르면 명령이 나온다:
    //   둘 잇기, 이웃끼리 잇기(격자 이웃을 그 순간 잇는 저작 명령), 선택끼리 끊기, 선 모두 지우기. 편집 규칙은 NodeTreeAuthoring에 있다.
    // - 검사: 고칠 때마다 게임과 같은 로더(NodeTreeLoader)로 불러 보고, 도구 검사(겹친 칸, 값을 줄이는 곱하기)를 더한다.
    //   진단을 누르면 그 노드로 간다.
    // - 구매 미리보기: 게임과 같은 규칙(NodeGraph의 드러남, NodePurchase의 구매)으로 노드를 사 보며 네 가지 상태와 업그레이드 합계를 본다.
    //   Gold는 무한이다. 산 노드를 다시 누르면 미리보기에서만 되돌린다(게임에는 환불이 없다).
    // 고친 내용은 Undo로 되돌릴 수 있고, 저장 버튼이나 프로젝트 저장으로 에셋에 쓴다.
    internal sealed class NodeTreeWindow : EditorWindow, INodeCanvasHost
    {
        private const long PreviewGold = long.MaxValue / 4;

        private static readonly Color NodeFill = new Color(0.25f, 0.32f, 0.45f);
        private static readonly Color StartFill = new Color(0.2f, 0.42f, 0.48f);
        private static readonly Color HiddenFill = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        private static readonly Color RevealedFill = new Color(0.38f, 0.38f, 0.4f);
        private static readonly Color PurchasableFill = new Color(0.62f, 0.5f, 0.12f);
        private static readonly Color OwnedFill = new Color(0.2f, 0.5f, 0.25f);
        private static readonly Color PlainBorder = new Color(0.08f, 0.08f, 0.1f);
        private static readonly Color StartBorder = new Color(0.4f, 0.85f, 0.95f);
        private static readonly Color SelectedBorder = Color.white;
        private static readonly Color ErrorBorder = new Color(0.95f, 0.3f, 0.3f);
        private static readonly Color ErrorText = new Color(1f, 0.55f, 0.55f);
        private static readonly Color WarningText = new Color(1f, 0.85f, 0.45f);

        [SerializeField] private NodeCatalog catalog;

        // 고른 노드의 ID(고른 순서). Undo가 노드 객체를 새로 만들 수 있어 ID로 기억한다.
        private readonly List<string> _selectedIds = new List<string>();
        private readonly List<string> _previewOwned = new List<string>();
        private readonly HashSet<string> _nodesWithErrors = new HashSet<string>(StringComparer.Ordinal);

        private NodeGridCanvas _canvas;
        private VisualElement _panel;
        private VisualElement _diagnosticsList;
        private (string A, string B)? _selectedLink;
        private string _message;
        private bool _preview;
        private NodeTreeLoadResult _load;
        private List<ContentDiagnostic> _toolChecks = new List<ContentDiagnostic>();
        private PlayerState _previewState;

        public NodeTreeData Tree => catalog != null ? catalog.Tree : null;
        public bool Editing => !_preview;

        public IReadOnlyCollection<NodeData> Selection => SelectedNodes(Tree);

        [MenuItem("BlackHole/Node Tree")]
        private static void Open() => GetWindow<NodeTreeWindow>("Node Tree");

        private void OnEnable() => Undo.undoRedoPerformed += Rebuild;

        private void OnDisable() => Undo.undoRedoPerformed -= Rebuild;

        private void CreateGUI()
        {
            if (catalog == null)
                catalog = FindCatalog();

            VisualElement root = rootVisualElement;

            var toolbar = new Toolbar();
            var catalogField = new ObjectField { objectType = typeof(NodeCatalog), allowSceneObjects = false, value = catalog };
            catalogField.style.minWidth = 220;
            catalogField.RegisterValueChangedCallback(evt =>
            {
                catalog = evt.newValue as NodeCatalog;
                ClearSelection();
                _previewOwned.Clear();
                Rebuild();
                _canvas.FrameAll();
            });
            toolbar.Add(catalogField);
            toolbar.Add(new ToolbarButton(() => _canvas.FrameAll()) { text = "전체 보기 (F)" });

            var previewToggle = new ToolbarToggle { text = "구매 미리보기" };
            previewToggle.RegisterValueChangedCallback(evt =>
            {
                _preview = evt.newValue;
                _previewOwned.Clear();
                Rebuild();
            });
            toolbar.Add(previewToggle);
            toolbar.Add(new ToolbarSpacer { flex = true });
            toolbar.Add(new ToolbarButton(Save) { text = "저장" });
            root.Add(toolbar);

            var split = new TwoPaneSplitView(1, 360, TwoPaneSplitViewOrientation.Horizontal);
            split.style.flexGrow = 1;

            var left = new VisualElement();
            left.style.flexGrow = 1;
            _canvas = new NodeGridCanvas(this);
            left.Add(_canvas);
            var help = new Label(
                "빈 칸 더블클릭: 놓기 · 클릭/박스: 고르기 (Ctrl: 더하기) · 끌기: 옮기기 · Shift+끌기: 잇기 · 선 클릭 후 Delete: 끊기 · 휠: 확대 · 가운데/오른쪽 끌기: 이동");
            help.style.whiteSpace = WhiteSpace.Normal;
            help.style.paddingLeft = 6;
            help.style.paddingTop = 2;
            help.style.paddingBottom = 2;
            left.Add(help);

            var side = new ScrollView();
            side.style.paddingLeft = 8;
            side.style.paddingRight = 8;
            _panel = new VisualElement();
            side.Add(_panel);
            side.Add(Header("검사"));
            _diagnosticsList = new VisualElement();
            side.Add(_diagnosticsList);

            split.Add(left);
            split.Add(side);
            root.Add(split);

            Rebuild();
        }

        private static NodeCatalog FindCatalog()
        {
            string[] guids = AssetDatabase.FindAssets("t:" + nameof(NodeCatalog));
            return guids.Length == 0 ? null : AssetDatabase.LoadAssetAtPath<NodeCatalog>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private void Save()
        {
            if (catalog != null)
                AssetDatabase.SaveAssetIfDirty(catalog);
        }

        #region 고른 것

        private List<NodeData> SelectedNodes(NodeTreeData tree)
        {
            var nodes = new List<NodeData>(_selectedIds.Count);

            if (tree == null)
                return nodes;

            foreach (string id in _selectedIds)
            {
                NodeData node = NodeTreeAuthoring.Find(tree, id);

                if (node != null)
                    nodes.Add(node);
            }

            return nodes;
        }

        private void Select(NodeData node, bool additive)
        {
            _selectedLink = null;
            _message = null;

            if (!additive)
            {
                _selectedIds.Clear();
                _selectedIds.Add(node.Id);
            }
            else if (!_selectedIds.Remove(node.Id))
            {
                _selectedIds.Add(node.Id);
            }
        }

        private void ClearSelection()
        {
            _selectedIds.Clear();
            _selectedLink = null;
            _message = null;
        }

        private bool LinkIs(NodeData a, NodeData b) =>
            _selectedLink.HasValue
            && ((_selectedLink.Value.A == a.Id && _selectedLink.Value.B == b.Id) || (_selectedLink.Value.A == b.Id && _selectedLink.Value.B == a.Id));

        #endregion

        #region 다시 그리기

        // 트리가 바뀔 때마다: 불러 보기 → 도구 검사 → 미리보기 상태 → 고른 것 정리 → 캔버스·패널·진단.
        private void Rebuild()
        {
            if (_canvas == null)
                return;

            _nodesWithErrors.Clear();
            _toolChecks = new List<ContentDiagnostic>();
            _load = null;

            if (catalog != null)
            {
                _load = NodeTreeLoader.Load(catalog.ToData());
                _toolChecks = NodeTreeAuthoring.Check(catalog.Tree);

                foreach (ContentDiagnostic diagnostic in _load.Diagnostics)
                    MarkError(diagnostic);

                foreach (ContentDiagnostic diagnostic in _toolChecks)
                    MarkError(diagnostic);

                ReplayPreview();
            }

            _selectedIds.RemoveAll(id => Tree == null || NodeTreeAuthoring.Find(Tree, id) == null);

            if (_selectedLink.HasValue)
            {
                NodeData a = Tree == null ? null : NodeTreeAuthoring.Find(Tree, _selectedLink.Value.A);
                NodeData b = Tree == null ? null : NodeTreeAuthoring.Find(Tree, _selectedLink.Value.B);

                if (a == null || b == null || !NodeTreeAuthoring.AreLinked(a, b))
                    _selectedLink = null;
            }

            _canvas.Refresh();
            BuildPanel();
            BuildDiagnostics();
        }

        // 고른 것만 바뀌었을 때.
        private void RefreshSelection()
        {
            _canvas.Refresh();
            BuildPanel();
        }

        // 미리보기에서 산 노드를 산 순서대로 다시 산다. 이제는 살 수 없는 노드(숨었거나 없어진 노드)는 빠진다.
        private void ReplayPreview()
        {
            _previewState = new PlayerState(new PlayerId(1));
            _previewState.EarnGold(PreviewGold);

            if (!_preview || _load?.Tree == null)
                return;

            var kept = new List<string>();

            foreach (string id in _previewOwned)
            {
                if (NodePurchase.TryPurchase(_previewState, _load.Tree, id) == PurchaseResult.Purchased)
                    kept.Add(id);
            }

            _previewOwned.Clear();
            _previewOwned.AddRange(kept);
        }

        private void MarkError(ContentDiagnostic diagnostic)
        {
            string id = NodeIdOf(diagnostic.Path);

            if (id != null)
                _nodesWithErrors.Add(id);
        }

        // "Nodes[a].Links[0]" → "a". 로더는 ID가 비어 있으면 순번을 쓴다.
        private string NodeIdOf(string path)
        {
            if (!path.StartsWith("Nodes[", StringComparison.Ordinal))
                return null;

            int end = path.IndexOf(']');

            if (end < 0)
                return null;

            string key = path.Substring(6, end - 6);

            if (Tree != null && NodeTreeAuthoring.Find(Tree, key) == null && int.TryParse(key, out int index)
                && index >= 0 && index < Tree.Nodes.Count)
                return Tree.Nodes[index].Id;

            return key;
        }

        #endregion

        #region 캔버스 입력

        public Color FillOf(NodeData node)
        {
            if (!_preview)
                return node.Start ? StartFill : NodeFill;

            if (_load?.Tree == null || !_load.Tree.TryGet(node.Id, out _))
                return HiddenFill;

            switch (NodePurchase.StateOf(_previewState, _load.Tree, node.Id))
            {
                case NodeState.Owned: return OwnedFill;
                case NodeState.Purchasable: return PurchasableFill;
                case NodeState.Revealed: return RevealedFill;
                default: return HiddenFill;
            }
        }

        public Color BorderOf(NodeData node)
        {
            if (!_preview && _selectedIds.Contains(node.Id))
                return SelectedBorder;

            if (node.Id != null && _nodesWithErrors.Contains(node.Id))
                return ErrorBorder;

            return node.Start ? StartBorder : PlainBorder;
        }

        public bool IsSelectedLink(NodeData a, NodeData b) => !_preview && LinkIs(a, b);

        public void OnEmptyCellClicked(int x, int y, int clickCount, bool additive)
        {
            if (_preview)
                return;

            if (clickCount >= 2)
            {
                Edit("노드 놓기", tree =>
                {
                    NodeData node = NodeTreeAuthoring.Place(tree, x, y);

                    if (node != null)
                        Select(node, false);
                });
                return;
            }

            if (!additive)
                ClearSelection();

            RefreshSelection();
        }

        public void OnNodeClicked(NodeData node, bool additive)
        {
            if (_preview)
            {
                // 미리보기에서만 되돌린다. 게임에는 환불이 없다.
                if (!_previewOwned.Remove(node.Id))
                    _previewOwned.Add(node.Id);

                Rebuild();
                return;
            }

            Select(node, additive);
            RefreshSelection();
        }

        public void OnLinkClicked(NodeData a, NodeData b)
        {
            if (_preview)
                return;

            _selectedIds.Clear();
            _message = null;
            _selectedLink = (a.Id, b.Id);
            RefreshSelection();
        }

        public void OnBoxSelected(List<NodeData> nodes, bool additive)
        {
            if (_preview)
                return;

            if (!additive)
                ClearSelection();

            _selectedLink = null;

            foreach (NodeData node in nodes)
            {
                if (!_selectedIds.Contains(node.Id))
                    _selectedIds.Add(node.Id);
            }

            RefreshSelection();
        }

        public void OnNodesDragged(IReadOnlyCollection<NodeData> nodes, int dx, int dy)
        {
            if (_preview)
                return;

            bool moved = false;
            Edit("노드 옮기기", tree => moved = NodeTreeAuthoring.MoveBy(tree, nodes, dx, dy));

            if (!moved)
                ShowMessage("옮길 칸에 다른 노드가 있어 옮기지 않았다.");
        }

        public void OnConnect(NodeData from, NodeData to)
        {
            if (_preview)
                return;

            Edit("선 잇기", _ => NodeTreeAuthoring.Link(from, to));
        }

        public void OnDeletePressed()
        {
            if (_preview)
                return;

            if (_selectedLink.HasValue)
            {
                NodeData a = NodeTreeAuthoring.Find(Tree, _selectedLink.Value.A);
                NodeData b = NodeTreeAuthoring.Find(Tree, _selectedLink.Value.B);

                if (a != null && b != null)
                    Edit("선 끊기", _ => NodeTreeAuthoring.Unlink(a, b));

                return;
            }

            List<NodeData> selected = SelectedNodes(Tree);

            if (selected.Count == 0)
                return;

            Edit("노드 지우기", tree =>
            {
                foreach (NodeData node in selected)
                    NodeTreeAuthoring.Remove(tree, node);
            });
        }

        // 모든 수정은 여기를 지난다: Undo 기록 → 수정 → 에셋 표시 → 다시 그리기.
        private void Edit(string undoName, Action<NodeTreeData> change)
        {
            Undo.RecordObject(catalog, undoName);
            change(catalog.Tree);
            EditorUtility.SetDirty(catalog);
            Rebuild();
        }

        private void ShowMessage(string message)
        {
            _message = message;
            BuildPanel();
        }

        #endregion

        #region 오른쪽 패널

        private void BuildPanel()
        {
            _panel.Clear();

            if (catalog == null)
            {
                _panel.Add(new HelpBox("노드 목록 에셋(NodeCatalog)을 고르세요.", HelpBoxMessageType.Info));
                return;
            }

            if (_preview)
            {
                BuildPreviewPanel();
                return;
            }

            if (_message != null)
                _panel.Add(new HelpBox(_message, HelpBoxMessageType.Warning));

            List<NodeData> selected = SelectedNodes(Tree);

            if (_selectedLink.HasValue)
                BuildLinkPanel();
            else if (selected.Count == 1)
                BuildNodePanel(selected[0]);
            else if (selected.Count > 1)
                BuildSelectionPanel(selected);
            else
                BuildSummary();
        }

        private void BuildNodePanel(NodeData node)
        {
            _panel.Add(Header("노드"));

            var id = new TextField("ID") { value = node.Id, isDelayed = true };
            id.RegisterValueChangedCallback(evt =>
            {
                string before = node.Id;
                bool renamed = false;
                Edit("ID 바꾸기", tree => renamed = NodeTreeAuthoring.Rename(tree, NodeTreeAuthoring.Find(tree, before), evt.newValue));

                if (renamed)
                {
                    ClearSelection();
                    _selectedIds.Add(evt.newValue.Trim());
                    RefreshSelection();
                }
                else
                {
                    ShowMessage($"'{evt.newValue}'로 바꿀 수 없다: 비었거나 이미 쓰는 ID다.");
                }
            });
            _panel.Add(id);
            _panel.Add(Note("ID는 산 노드를 기록하는 저장 키다. 플레이어가 산 뒤에는 바꾸지 않는다."));

            var price = new LongField("가격") { value = node.Price, isDelayed = true };
            price.RegisterValueChangedCallback(evt => Edit("가격 바꾸기", tree => NodeTreeAuthoring.Find(tree, node.Id).Price = evt.newValue));
            _panel.Add(price);

            var start = new Toggle("시작 노드") { value = node.Start };
            start.RegisterValueChangedCallback(evt => Edit("시작 노드 바꾸기", tree => NodeTreeAuthoring.Find(tree, node.Id).Start = evt.newValue));
            _panel.Add(start);

            _panel.Add(new Label($"칸 ({node.X}, {node.Y})"));

            _panel.Add(Header("이어진 노드"));
            bool anyLink = false;

            foreach (NodeData other in Tree.Nodes)
            {
                if (other == node || !NodeTreeAuthoring.AreLinked(node, other))
                    continue;

                anyLink = true;
                NodeData target = other;
                var row = Row();
                row.Add(Grow(new Label(target.Id)));
                row.Add(new Button(() => Edit("선 끊기", _ => NodeTreeAuthoring.Unlink(node, target))) { text = "끊기" });
                _panel.Add(row);
            }

            if (!anyLink)
                _panel.Add(Note("없음. Shift+끌기로 다른 노드와 잇는다."));

            _panel.Add(Header("업그레이드"));

            for (int i = 0; i < node.Upgrades.Count; i++)
                _panel.Add(UpgradeRow(node, i));

            _panel.Add(new Button(() => Edit("업그레이드 더하기", tree =>
                NodeTreeAuthoring.Find(tree, node.Id).Upgrades.Add(new UpgradeData { Stat = string.Empty, Operation = UpgradeOperation.Add, Value = 1 })))
            {
                text = "업그레이드 더하기",
            });

            var remove = new Button(OnDeletePressed) { text = "노드 지우기" };
            remove.style.marginTop = 12;
            _panel.Add(remove);
        }

        // 업그레이드 한 줄: 수치 이름, 연산, 값, 값의 뜻(+1 / +25% / ×10), 지우기.
        private VisualElement UpgradeRow(NodeData node, int index)
        {
            UpgradeData upgrade = node.Upgrades[index];
            string nodeId = node.Id;
            var box = new VisualElement();
            box.style.marginBottom = 6;

            var stat = new TextField("수치") { value = upgrade.Stat, isDelayed = true };
            stat.RegisterValueChangedCallback(evt => Edit("수치 이름 바꾸기", tree => UpgradeAt(tree, nodeId, index).Stat = evt.newValue));
            box.Add(stat);

            var row = Row();
            var operation = new EnumField(upgrade.Operation);
            operation.style.width = 90;
            operation.RegisterValueChangedCallback(evt => Edit("연산 바꾸기", tree => UpgradeAt(tree, nodeId, index).Operation = (UpgradeOperation)evt.newValue));
            row.Add(operation);

            var value = new FloatField { value = upgrade.Value, isDelayed = true };
            value.style.width = 70;
            value.RegisterValueChangedCallback(evt => Edit("값 바꾸기", tree => UpgradeAt(tree, nodeId, index).Value = evt.newValue));
            row.Add(value);

            var meaning = new Label(NodeTreeAuthoring.Notation(upgrade.Operation, upgrade.Value));
            meaning.style.unityTextAlign = TextAnchor.MiddleLeft;
            meaning.style.minWidth = 60;
            meaning.style.marginLeft = 6;
            row.Add(Grow(meaning));

            row.Add(new Button(() => Edit("업그레이드 지우기", tree => NodeTreeAuthoring.Find(tree, nodeId).Upgrades.RemoveAt(index))) { text = "지우기" });
            box.Add(row);
            return box;
        }

        private static UpgradeData UpgradeAt(NodeTreeData tree, string nodeId, int index) =>
            NodeTreeAuthoring.Find(tree, nodeId).Upgrades[index];

        // 여러 노드를 골랐을 때: 저작 명령.
        private void BuildSelectionPanel(List<NodeData> selected)
        {
            _panel.Add(Header($"노드 {selected.Count}개 고름"));

            var connectTwo = new Button(() => Edit("둘 잇기", _ => NodeTreeAuthoring.Link(selected[0], selected[1]))) { text = "둘 잇기" };
            connectTwo.SetEnabled(selected.Count == 2);
            _panel.Add(connectTwo);

            _panel.Add(new Button(() => Command("이웃끼리 잇기", tree => NodeTreeAuthoring.LinkNeighbors(selected), "선 {0}개를 이었다.")) { text = "이웃끼리 잇기" });
            _panel.Add(Note("고른 노드 가운데 상하좌우로 붙은 쌍을 지금 잇는다. 나중에 옮겨도 선은 그대로다."));
            _panel.Add(new Button(() => Command("선택끼리 끊기", tree => NodeTreeAuthoring.UnlinkAmong(selected), "선 {0}개를 끊었다.")) { text = "선택끼리 끊기" });
            _panel.Add(new Button(() => Command("선 모두 지우기", tree => NodeTreeAuthoring.ClearLinks(tree, selected), "선 {0}개를 지웠다.")) { text = "선 모두 지우기" });
            _panel.Add(Note("선택끼리 끊기: 고른 노드 사이의 선만. 선 모두 지우기: 고른 노드에 닿은 선 전부."));

            var remove = new Button(OnDeletePressed) { text = "노드 지우기" };
            remove.style.marginTop = 12;
            _panel.Add(remove);
        }

        private void Command(string undoName, Func<NodeTreeData, int> change, string resultFormat)
        {
            int count = 0;
            Edit(undoName, tree => count = change(tree));
            ShowMessage(string.Format(CultureInfo.InvariantCulture, resultFormat, count));
        }

        private void BuildLinkPanel()
        {
            (string a, string b) = _selectedLink.Value;
            _panel.Add(Header("선"));
            _panel.Add(new Label($"{a} — {b}"));
            _panel.Add(new Button(OnDeletePressed) { text = "끊기 (Delete)" });
        }

        private void BuildSummary()
        {
            int starts = 0;

            foreach (NodeData node in Tree.Nodes)
            {
                if (node.Start)
                    starts++;
            }

            _panel.Add(Header("트리"));
            _panel.Add(new Label($"노드 {Tree.Nodes.Count}개 · 시작 노드 {starts}개 · 선 {NodeTreeAuthoring.Links(Tree).Count}개"));
            _panel.Add(Note("빈 칸을 더블클릭하면 노드를 놓는다. 노드를 여럿 고르면(박스·Ctrl+클릭) 이웃끼리 잇기 같은 명령이 나온다."));
        }

        // 미리보기: 산 노드 수, 쓴 Gold, 수치별 업그레이드 합계. 가져가는 시스템의 기본값은 모르므로 기본값 0과 1일 때를 보여 준다.
        private void BuildPreviewPanel()
        {
            _panel.Add(Header("구매 미리보기"));

            if (_load?.Tree == null)
            {
                _panel.Add(new HelpBox("트리를 불러오지 못해 미리볼 수 없다. 아래 검사를 먼저 고친다.", HelpBoxMessageType.Error));
                return;
            }

            _panel.Add(new Label($"산 노드 {_previewOwned.Count} / {_load.Tree.Nodes.Count} · 쓴 Gold {PreviewGold - _previewState.Gold}"));
            _panel.Add(Note("노랑: 살 수 있음 · 초록: 산 것 · 회색: 보이지만 못 삼 · 어두움: 숨김. 산 노드를 다시 누르면 미리보기에서만 되돌린다(게임에는 환불이 없다)."));

            UpgradeTable table = NodePurchase.UpgradesFor(_previewState, _load.Tree);
            var stats = new SortedSet<string>(StringComparer.Ordinal);

            foreach (NodeDefinition node in _load.Tree.Nodes)
            {
                foreach (Upgrade upgrade in node.Upgrades)
                    stats.Add(upgrade.Stat);
            }

            _panel.Add(Header("업그레이드 합계"));

            foreach (string stat in stats)
                _panel.Add(new Label($"{stat}: 기본값 0 → {Number(table.Apply(stat, 0))}, 기본값 1 → {Number(table.Apply(stat, 1))}"));
        }

        private void BuildDiagnostics()
        {
            _diagnosticsList.Clear();

            if (catalog == null)
                return;

            if (_load.Diagnostics.Count == 0 && _toolChecks.Count == 0)
            {
                _diagnosticsList.Add(new Label("문제 없음") { style = { color = new Color(0.5f, 0.9f, 0.5f) } });
                return;
            }

            foreach (ContentDiagnostic diagnostic in _load.Diagnostics)
                _diagnosticsList.Add(DiagnosticButton(diagnostic, ErrorText));

            foreach (ContentDiagnostic diagnostic in _toolChecks)
                _diagnosticsList.Add(DiagnosticButton(diagnostic, WarningText));
        }

        private Button DiagnosticButton(ContentDiagnostic diagnostic, Color color)
        {
            var button = new Button(() =>
            {
                NodeData node = NodeTreeAuthoring.Find(Tree, NodeIdOf(diagnostic.Path));

                if (node == null)
                    return;

                Select(node, false);
                _canvas.CenterOn(node);
                BuildPanel();
            })
            {
                text = diagnostic.ToString(),
            };

            button.style.color = color;
            button.style.whiteSpace = WhiteSpace.Normal;
            button.style.unityTextAlign = TextAnchor.MiddleLeft;
            return button;
        }

        private static Label Header(string text)
        {
            var label = new Label(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginTop = 10;
            label.style.marginBottom = 4;
            return label;
        }

        private static Label Note(string text)
        {
            var label = new Label(text);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.opacity = 0.7f;
            label.style.marginBottom = 4;
            return label;
        }

        private static VisualElement Row()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            return row;
        }

        private static T Grow<T>(T element) where T : VisualElement
        {
            element.style.flexGrow = 1;
            return element;
        }

        private static string Number(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        #endregion
    }
}
