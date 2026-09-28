using System;
using System.Collections.Generic;
using System.Globalization;
using BlackHole.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BlackHole.Unity
{
    // 업그레이드 화면의 트리 보기(uGUI).
    //
    // 좌표: 칸 (X, Y)의 노드는 트리 공간의 (X × 칸 크기, Y × 칸 크기)에 놓인다.
    // 보이기: 숨은 노드는 그리지 않는다. 선은 양 끝이 모두 보일 때만 그리고, 양 끝을 모두 샀으면 밝게 칠함.
    // 조작: 빈 곳이나 노드 위를 끌면 이동, 휠은 커서를 중심으로 확대·축소.
    //
    // 업그레이드 화면(UpgradeScreen)을 호스트로 두는 페이지(UIPage)다.
    // ScreenFlow가 업그레이드 화면을 연 뒤 SwitchPage로 염.
    // 이 컴포넌트는 트리 영역(보이는 창)에 붙고, 그 영역은 호스트의 PageRoot 바로 아래 자식.
    //
    // 영역은 가운데 기준(pivot 0.5)이어야 확대 계산이 맞음.
    public sealed class NodeTreeView : UIPage<NodeTreeView.Refs>, IBeginDragHandler, IDragHandler, IScrollHandler
    {
        public enum Refs { }

        // 노드 하나를 그리는 데 필요한 것.
        public readonly struct NodeItem
        {
            public string Id { get; }
            public int X { get; }
            public int Y { get; }
            public long Price { get; }

            public NodeItem(string id, int x, int y, long price)
            {
                Id = id;
                X = x;
                Y = y;
                Price = price;
            }
        }

        private const float CellSize = 120;
        private const float NodeSize = 84;
        private const float LinkWidth = 8;
        private const float MinZoom = 0.3f;
        private const float MaxZoom = 2.5f;
        private const float FitZoomLimit = 1.5f;

        private static readonly Color OwnedColor = new Color(0.22f, 0.58f, 0.32f);
        private static readonly Color PurchasableColor = new Color(0.85f, 0.66f, 0.18f);
        private static readonly Color RevealedColor = new Color(0.32f, 0.33f, 0.37f);
        private static readonly Color OwnedLinkColor = new Color(0.72f, 0.9f, 0.76f);
        private static readonly Color LinkColor = new Color(0.45f, 0.47f, 0.52f);

        private readonly Dictionary<string, NodeVisual> _nodes = new Dictionary<string, NodeVisual>(StringComparer.Ordinal);
        private readonly List<LinkVisual> _links = new List<LinkVisual>();

        private RectTransform _viewport;
        private RectTransform _content;
        private RectTransform _linkLayer;
        private RectTransform _nodeLayer;
        private Canvas _canvas;
        private float _zoom = 1;
        private bool _framePending;

        public event Action<string> NodeClicked;

        protected override void OnInitialize()
        {
            _viewport = (RectTransform)transform;

            // 끌기·휠을 받으려면 트리 영역 자체가 레이캐스트 대상이어야 한다. 보이지 않는 이미지를 깜.
            if (GetComponent<Graphic>() == null)
                gameObject.AddComponent<Image>().color = Color.clear;

            if (GetComponent<RectMask2D>() == null)
                gameObject.AddComponent<RectMask2D>();

            _content = Layer(_viewport, "Content");
            _linkLayer = Layer(_content, "Links");
            _nodeLayer = Layer(_content, "Nodes");
        }

        // 트리를 새로 만든다. 선의 양 끝 가운데 하나라도 노드 목록에 없으면 그 선은 그리지 않음.
        public void Build(IReadOnlyList<NodeItem> nodes, IReadOnlyList<(string A, string B)> links)
        {
            Clear();

            foreach (NodeItem node in nodes)
                _nodes.Add(node.Id, CreateNode(node));

            foreach ((string a, string b) in links)
            {
                if (_nodes.TryGetValue(a, out NodeVisual from) && _nodes.TryGetValue(b, out NodeVisual to))
                    _links.Add(CreateLink(from, to));
            }

            _framePending = true;
        }

        // 노드마다 상태를 받아 칠한다. 바뀐 프레임에만 호출.
        public void Show(IReadOnlyDictionary<string, NodeState> states)
        {
            foreach (NodeVisual node in _nodes.Values)
            {
                node.State = states[node.Id];
                bool visible = node.State != NodeState.Hidden;
                node.Root.SetActive(visible);

                if (!visible)
                    continue;

                node.Image.color = ColorOf(node.State);
                node.Button.interactable = node.State == NodeState.Purchasable;
                node.Label.text = node.State == NodeState.Owned
                    ? $"{node.Id}\nowned"
                    : $"{node.Id}\n{node.Price.ToString("N0", CultureInfo.InvariantCulture)}";
            }

            foreach (LinkVisual link in _links)
            {
                bool visible = link.From.State != NodeState.Hidden && link.To.State != NodeState.Hidden;
                link.Root.SetActive(visible);

                if (visible)
                    link.Image.color = link.From.State == NodeState.Owned && link.To.State == NodeState.Owned ? OwnedLinkColor : LinkColor;
            }
        }

        // 모든 노드(숨은 노드 포함)의 범위가 보이게 맞춘다. 저작한 모양의 가운데가 화면 가운데임.
        public void FrameAll()
        {
            Rect view = _viewport.rect;

            if (_nodes.Count == 0 || view.width <= 0 || view.height <= 0)
            {
                SetZoom(1);
                _content.anchoredPosition = Vector2.zero;
                return;
            }

            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);

            foreach (NodeVisual node in _nodes.Values)
            {
                min = Vector2.Min(min, node.Position);
                max = Vector2.Max(max, node.Position);
            }

            Vector2 size = max - min + new Vector2(NodeSize + CellSize, NodeSize + CellSize);
            SetZoom(Mathf.Clamp(Mathf.Min(view.width / size.x, view.height / size.y), MinZoom, FitZoomLimit));
            _content.anchoredPosition = -(min + max) * 0.5f * _zoom;
        }

        // 화면이 열린 직후에는 영역 크기가 아직 0일 수 있다. 크기가 생긴 뒤 한 번 맞춤.
        private void LateUpdate()
        {
            if (_framePending && _viewport.rect.width > 0)
            {
                _framePending = false;
                FrameAll();
            }
        }

        public void OnBeginDrag(PointerEventData eventData) { }

        public void OnDrag(PointerEventData eventData) =>
            _content.anchoredPosition += eventData.delta / ScaleFactor();

        // 커서 아래의 점이 그대로 있도록 확대.
        public void OnScroll(PointerEventData eventData)
        {
            if (Mathf.Approximately(eventData.scrollDelta.y, 0))
                return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, eventData.position, eventData.enterEventCamera, out Vector2 cursor))
                return;

            float before = _zoom;
            SetZoom(Mathf.Clamp(_zoom * (eventData.scrollDelta.y > 0 ? 1.1f : 1 / 1.1f), MinZoom, MaxZoom));
            _content.anchoredPosition = cursor - (cursor - _content.anchoredPosition) * (_zoom / before);
        }

        private void SetZoom(float zoom)
        {
            _zoom = zoom;
            _content.localScale = new Vector3(zoom, zoom, 1);
        }

        private float ScaleFactor()
        {
            if (_canvas == null)
                _canvas = GetComponentInParent<Canvas>().rootCanvas;

            return _canvas.scaleFactor;
        }

        private static Color ColorOf(NodeState state)
        {
            switch (state)
            {
                case NodeState.Owned: return OwnedColor;
                case NodeState.Purchasable: return PurchasableColor;
                default: return RevealedColor;
            }
        }

        #region 만들기

        private NodeVisual CreateNode(NodeItem item)
        {
            RectTransform rect = Child(_nodeLayer, "Node " + item.Id);
            rect.sizeDelta = new Vector2(NodeSize, NodeSize);
            var position = new Vector2(item.X * CellSize, item.Y * CellSize);
            rect.anchoredPosition = position;

            var image = rect.gameObject.AddComponent<Image>();
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            // 색은 상태로 칠한다. 버튼의 눌림·꺼짐 색이 덮지 않게 한다.
            button.transition = Selectable.Transition.None;
            string id = item.Id;
            button.onClick.AddListener(() => NodeClicked?.Invoke(id));

            RectTransform labelRect = Child(rect, "Label");
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(4, 4);
            labelRect.offsetMax = new Vector2(-4, -4);
            var label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            // 긴 ID도 가격 줄까지 보이도록 칸에 맞게 글자를 줄인다.
            label.enableAutoSizing = true;
            label.fontSizeMin = 10;
            label.fontSizeMax = 18;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;

            return new NodeVisual(item.Id, item.Price, position, rect.gameObject, image, button, label);
        }

        // 두 노드 중심을 잇는 얇은 막대: 가운데에 놓고, 길이만큼 늘리고, 방향만큼 돌린다.
        private LinkVisual CreateLink(NodeVisual from, NodeVisual to)
        {
            RectTransform rect = Child(_linkLayer, $"Link {from.Id}-{to.Id}");
            Vector2 delta = to.Position - from.Position;
            rect.anchoredPosition = (from.Position + to.Position) * 0.5f;
            rect.sizeDelta = new Vector2(delta.magnitude, LinkWidth);
            rect.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

            var image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            return new LinkVisual(from, to, rect.gameObject, image);
        }

        private void Clear()
        {
            foreach (NodeVisual node in _nodes.Values)
                Destroy(node.Root);

            foreach (LinkVisual link in _links)
                Destroy(link.Root);

            _nodes.Clear();
            _links.Clear();
        }

        private static RectTransform Layer(RectTransform parent, string name)
        {
            RectTransform rect = Child(parent, name);
            rect.sizeDelta = Vector2.zero;
            return rect;
        }

        // 부모 가운데에 붙는 자식.
        private static RectTransform Child(RectTransform parent, string name)
        {
            var child = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)child.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            return rect;
        }

        private sealed class NodeVisual
        {
            public readonly string Id;
            public readonly long Price;
            public readonly Vector2 Position;
            public readonly GameObject Root;
            public readonly Image Image;
            public readonly Button Button;
            public readonly TMP_Text Label;
            public NodeState State = NodeState.Hidden;

            public NodeVisual(string id, long price, Vector2 position, GameObject root, Image image, Button button, TMP_Text label)
            {
                Id = id;
                Price = price;
                Position = position;
                Root = root;
                Image = image;
                Button = button;
                Label = label;
            }
        }

        private sealed class LinkVisual
        {
            public readonly NodeVisual From;
            public readonly NodeVisual To;
            public readonly GameObject Root;
            public readonly Image Image;

            public LinkVisual(NodeVisual from, NodeVisual to, GameObject root, Image image)
            {
                From = from;
                To = to;
                Root = root;
                Image = image;
            }
        }

        #endregion
    }
}
