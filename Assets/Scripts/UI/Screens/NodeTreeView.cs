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
    // 보이기: 숨은 노드는 그리지 않는다. 선은 양 끝이 모두 보일 때만 그린다.
    // 모양: 노드 그림·선 색·가격 색은 NodeTreeLook이 정한다(스탯마다의 노드 그림). 가격은 노드 아래에 쓴다.
    //   Look이 비어 있으면 예전처럼 상태 색 칸에 ID·가격을 쓴다.
    // 조작: 빈 곳이나 노드 위를 끌면 이동, 휠은 커서를 중심으로 확대·축소.
    // 올림: 포인터 아래의 노드가 바뀌면 NodeHovered·NodeLeft로 알린다(툴팁은 호스트가 띄운다).
    //   노드마다 입력 컴포넌트를 붙이지 않고 트리 영역이 포인터 이동을 받아 찾는다 — UI_EventHandler를 노드에 붙이면
    //   노드 위에서 시작한 끌기를 그것이 가져가 트리가 움직이지 않는다. 끌기·휠이 시작되면 올림을 푼다.
    //
    // 업그레이드 화면(UpgradeScreen)을 호스트로 두는 페이지(UIPage)다.
    // ScreenFlow가 업그레이드 화면을 연 뒤 SwitchPage로 염.
    // 이 컴포넌트는 트리 영역(보이는 창)에 붙고, 그 영역은 호스트의 PageRoot 바로 아래 자식.
    //
    // 영역은 가운데 기준(pivot 0.5)이어야 확대 계산이 맞음.
    public sealed class NodeTreeView : UIPage<NodeTreeView.Refs>, IBeginDragHandler, IDragHandler, IScrollHandler,
        IPointerMoveHandler, IPointerExitHandler
    {
        public enum Refs { }

        // 노드 하나를 그리는 데 필요한 것.
        public readonly struct NodeItem
        {
            public string Id { get; }
            public int X { get; }
            public int Y { get; }
            // 처음 그릴 때의 가격(Rank 1 비용). 이후에는 Show가 다음 Rank 비용으로 바꾼다.
            public long Price { get; }
            // 노드 그림을 고르는 스탯 키(Rank 1 첫 효과의 StatId). 없으면 기본 그림.
            public string Stat { get; }

            public NodeItem(string id, int x, int y, long price, string stat = null)
            {
                Id = id;
                X = x;
                Y = y;
                Price = price;
                Stat = stat;
            }
        }

        private const float CellSize = 150;
        private const float NodeSize = 96;
        private const float LinkWidth = 6;
        // 노드 아래 가격 딱지(글자 폭 + 여백). 선이 지나가도 가격이 읽히도록 바탕을 깐다.
        private const float PriceHeight = 30;
        private const float PriceSize = 24;
        private const float PricePadding = 16;
        private const float MinZoom = 0.3f;
        private const float MaxZoom = 2.5f;
        private const float FitZoomLimit = 1.5f;

        private static readonly Color OwnedColor = new Color(0.22f, 0.58f, 0.32f);
        private static readonly Color PurchasableColor = new Color(0.85f, 0.66f, 0.18f);
        private static readonly Color RevealedColor = new Color(0.32f, 0.33f, 0.37f);
        private static readonly Color OwnedLinkColor = new Color(0.72f, 0.9f, 0.76f);
        private static readonly Color LinkColor = new Color(0.45f, 0.47f, 0.52f);

        [Tooltip("노드 그림과 선·가격 색. 비우면 상태 색 칸으로 그린다.")]
        [SerializeField] private NodeTreeLook _look;

        private readonly Dictionary<string, NodeVisual> _nodes = new Dictionary<string, NodeVisual>(StringComparer.Ordinal);
        private readonly Dictionary<GameObject, NodeVisual> _nodesByObject = new Dictionary<GameObject, NodeVisual>();
        private readonly List<LinkVisual> _links = new List<LinkVisual>();

        private RectTransform _viewport;
        private RectTransform _content;
        private RectTransform _linkLayer;
        private RectTransform _nodeLayer;
        private Canvas _canvas;
        private float _zoom = 1;
        private bool _framePending;

        public event Action<string> NodeClicked;

        // 포인터가 노드 위에 올라감(노드 ID, 노드 칸)과 벗어남(노드 ID). 올라간 노드는 하나뿐이고, 벗어남이 먼저 온다.
        public event Action<string, RectTransform> NodeHovered;
        public event Action<string> NodeLeft;

        private NodeVisual _hovered;

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
            {
                NodeVisual visual = CreateNode(node);
                _nodes.Add(node.Id, visual);
                _nodesByObject.Add(visual.Root, visual);
            }

            foreach ((string a, string b) in links)
            {
                if (_nodes.TryGetValue(a, out NodeVisual from) && _nodes.TryGetValue(b, out NodeVisual to))
                    _links.Add(CreateLink(from, to));
            }

            _framePending = true;
        }

        // 노드마다 상태(와 다음 Rank 비용)를 받아 칠한다. 바뀐 프레임에만 호출.
        // costs에 없는 노드(마지막 Rank까지 산 노드)는 가격을 바꾸지 않는다 — Owned면 가격을 쓰지 않는다.
        public void Show(IReadOnlyDictionary<string, NodeState> states, IReadOnlyDictionary<string, long> costs = null)
        {
            foreach (NodeVisual node in _nodes.Values)
            {
                node.State = states[node.Id];

                if (costs != null && costs.TryGetValue(node.Id, out long cost))
                    node.Price = cost;
                bool visible = node.State != NodeState.Hidden;
                node.Root.SetActive(visible);

                if (!visible)
                    continue;

                node.Button.interactable = node.State == NodeState.Purchasable;
                Paint(node);
            }

            // 올라가 있던 노드가 숨으면 올림을 푼다.
            if (_hovered != null && _hovered.State == NodeState.Hidden)
                SetHovered(null);

            foreach (LinkVisual link in _links)
            {
                bool visible = link.From.State != NodeState.Hidden && link.To.State != NodeState.Hidden;
                link.Root.SetActive(visible);

                if (!visible)
                    continue;

                if (_look != null)
                    link.Image.color = _look.LinkColorOf(link.From.State, link.To.State);
                else
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

        public void OnBeginDrag(PointerEventData eventData) => SetHovered(null);

        public void OnDrag(PointerEventData eventData) =>
            _content.anchoredPosition += eventData.delta / ScaleFactor();

        // 커서 아래의 점이 그대로 있도록 확대.
        public void OnScroll(PointerEventData eventData)
        {
            SetHovered(null);

            if (Mathf.Approximately(eventData.scrollDelta.y, 0))
                return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, eventData.position, eventData.enterEventCamera, out Vector2 cursor))
                return;

            float before = _zoom;
            SetZoom(Mathf.Clamp(_zoom * (eventData.scrollDelta.y > 0 ? 1.1f : 1 / 1.1f), MinZoom, MaxZoom));
            _content.anchoredPosition = cursor - (cursor - _content.anchoredPosition) * (_zoom / before);
        }

        // 포인터 아래의 노드(레이캐스트를 받는 것은 노드 칸뿐이다). 끄는 중에는 바꾸지 않는다.
        public void OnPointerMove(PointerEventData eventData)
        {
            if (eventData.dragging)
                return;

            GameObject target = eventData.pointerCurrentRaycast.gameObject;
            SetHovered(target != null && _nodesByObject.TryGetValue(target, out NodeVisual node) ? node : null);
        }

        // 트리 영역을 벗어났을 때. 영역 안의 노드로 옮겨 간 것이면 그대로 둔다.
        public void OnPointerExit(PointerEventData eventData)
        {
            GameObject target = eventData.pointerCurrentRaycast.gameObject;
            if (target == null || !target.transform.IsChildOf(transform))
                SetHovered(null);
        }

        // 페이지가 닫히면(비활성) 올림을 푼다. 다시 열 때 남아 있지 않게 한다.
        private void OnDisable() => SetHovered(null);

        private void SetHovered(NodeVisual node)
        {
            if (node == _hovered)
                return;

            if (_hovered != null)
            {
                NodeVisual left = _hovered;
                _hovered = null;
                Paint(left);
                NodeLeft?.Invoke(left.Id);
            }

            if (node == null)
                return;

            _hovered = node;
            Paint(node);
            NodeHovered?.Invoke(node.Id, (RectTransform)node.Root.transform);
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

        // 노드 하나를 지금 상태(와 올림)대로 칠한다. 가격은 사지 않은 노드에만 쓴다.
        private void Paint(NodeVisual node)
        {
            if (node.State == NodeState.Hidden)
                return;

            string price = node.Price.ToString("N0", CultureInfo.InvariantCulture);

            if (_look == null)
            {
                node.Image.sprite = null;
                node.Image.color = ColorOf(node.State);
                node.Label.color = Color.white;
                node.Label.text = node.State == NodeState.Owned ? $"{node.Id}\nowned" : $"{node.Id}\n{price}";
                return;
            }

            node.Image.sprite = _look.SpriteOf(node.Stat, node.State, node == _hovered);
            node.Image.color = _look.TintOf(node.State);

            bool showPrice = node.State != NodeState.Owned;
            if (node.Badge != null && node.Badge.gameObject.activeSelf != showPrice)
                node.Badge.gameObject.SetActive(showPrice);

            if (!showPrice || node.Label.text == price)
            {
                node.Label.color = _look.PriceColorOf(node.State);
                return;
            }

            node.Label.color = _look.PriceColorOf(node.State);
            node.Label.text = price;

            if (node.Badge != null)
                node.Badge.sizeDelta = new Vector2(node.Label.GetPreferredValues(price).x + PricePadding, PriceHeight);
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
            image.preserveAspect = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            // 색은 상태로 칠한다. 버튼의 눌림·꺼짐 색이 덮지 않게 한다.
            button.transition = Selectable.Transition.None;
            string id = item.Id;
            button.onClick.AddListener(() => NodeClicked?.Invoke(id));

            RectTransform badge = null;
            RectTransform labelRect;

            if (_look != null)
            {
                // 가격 딱지: 노드 바로 아래 가운데. 폭은 칠할 때 글자에 맞춘다.
                badge = Child(rect, "Price");
                badge.anchorMin = new Vector2(0.5f, 0);
                badge.anchorMax = new Vector2(0.5f, 0);
                badge.pivot = new Vector2(0.5f, 1);
                badge.sizeDelta = new Vector2(NodeSize, PriceHeight);
                var back = badge.gameObject.AddComponent<Image>();
                back.color = _look.PriceBackColor;
                back.raycastTarget = false;
                labelRect = Child(badge, "Label");
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.sizeDelta = Vector2.zero;
            }
            else
            {
                labelRect = Child(rect, "Label");
            }

            var label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();

            if (_look != null)
            {
                label.fontSize = PriceSize;
            }
            else
            {
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(4, 4);
                labelRect.offsetMax = new Vector2(-4, -4);
                // 긴 ID도 가격 줄까지 보이도록 칸에 맞게 글자를 줄인다.
                label.enableAutoSizing = true;
                label.fontSizeMin = 10;
                label.fontSizeMax = 18;
            }

            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;

            return new NodeVisual(item.Id, item.Stat, item.Price, position, rect.gameObject, image, button, label, badge);
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
            SetHovered(null);

            foreach (NodeVisual node in _nodes.Values)
                Destroy(node.Root);

            foreach (LinkVisual link in _links)
                Destroy(link.Root);

            _nodes.Clear();
            _nodesByObject.Clear();
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
            public readonly string Stat;
            // 다음 Rank의 비용.
            public long Price;
            public readonly Vector2 Position;
            public readonly GameObject Root;
            public readonly Image Image;
            public readonly Button Button;
            public readonly TMP_Text Label;
            // 가격 딱지(Look이 있을 때만). 샀으면 숨긴다.
            public readonly RectTransform Badge;
            public NodeState State = NodeState.Hidden;

            public NodeVisual(string id, string stat, long price, Vector2 position, GameObject root, Image image, Button button, TMP_Text label,
                RectTransform badge)
            {
                Id = id;
                Stat = stat;
                Price = price;
                Position = position;
                Root = root;
                Image = image;
                Button = button;
                Label = label;
                Badge = badge;
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
