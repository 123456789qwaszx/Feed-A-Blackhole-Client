using System;
using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 업그레이드 트리의 모양: 스탯마다의 노드 그림(BlackHoleGUI Stat Nodes)과 선·가격 글자의 색.
    // 노드는 자기 첫 업그레이드의 스탯 키로 그림을 찾는다. 모르는 스탯은 기본 그림을 쓴다.
    // 트리 보기(NodeTreeView)가 들고 있고, 비어 있으면 트리 보기는 예전처럼 색 칸으로 그린다.
    [CreateAssetMenu(fileName = "NodeTreeLook", menuName = "BlackHole/Node Tree Look")]
    public sealed class NodeTreeLook : ScriptableObject
    {
        [Serializable]
        public sealed class NodeSprites
        {
            [Tooltip("스탯 키. 예: breaker.damage")]
            public string stat;
            [Tooltip("살 수 있음.")]
            public Sprite available;
            [Tooltip("손을 올림(PC).")]
            public Sprite hover;
            [Tooltip("샀음.")]
            public Sprite purchased;
            [Tooltip("잠김: 내용 대신 자물쇠.")]
            public Sprite locked;
        }

        [Tooltip("스탯을 모르거나 그 상태의 그림이 비었을 때 쓰는 그림.")]
        [SerializeField] private NodeSprites _fallback = new NodeSprites();
        [SerializeField] private List<NodeSprites> _stats = new List<NodeSprites>();

        [Header("보이지만 Gold가 모자란 노드(Revealed)")]
        [Tooltip("살 수 있음 그림에 곱하는 색. 흐리게 보인다.")]
        [SerializeField] private Color _revealedTint = new Color(1, 1, 1, 0.5f);
        [Tooltip("켜면 흐린 그림 대신 자물쇠(locked) 그림을 쓴다. 무엇인지 숨기고 싶을 때.")]
        [SerializeField] private bool _revealedAsLocked;

        [Header("선")]
        [Tooltip("양 끝을 모두 샀다.")]
        [SerializeField] private Color _ownedLinkColor = new Color(0.341f, 0.592f, 0.412f);
        [Tooltip("한쪽을 샀고 다른 쪽을 지금 살 수 있다.")]
        [SerializeField] private Color _openLinkColor = new Color(0.125f, 0.129f, 0.141f);
        [Tooltip("그 밖(Gold가 모자람 등).")]
        [SerializeField] private Color _lockedLinkColor = new Color(0.722f, 0.729f, 0.725f);

        [Header("가격 글자")]
        [SerializeField] private Color _priceColor = new Color(0.125f, 0.129f, 0.141f);
        [SerializeField] private Color _unaffordablePriceColor = new Color(0.42f, 0.435f, 0.424f);
        [Tooltip("가격 딱지의 바탕. 선이 지나가도 가격이 읽히게 한다.")]
        [SerializeField] private Color _priceBackColor = new Color(0.953f, 0.945f, 0.91f);

        public Color PriceBackColor => _priceBackColor;

        private Dictionary<string, NodeSprites> _byStat;

        // 노드 그림. hovered는 손을 올린 노드(샀으면 무시한다).
        public Sprite SpriteOf(string stat, NodeState state, bool hovered)
        {
            NodeSprites sprites = Find(stat);

            switch (state)
            {
                case NodeState.Owned:
                    return Pick(sprites?.purchased, _fallback.purchased);
                case NodeState.Revealed when _revealedAsLocked:
                    return Pick(sprites?.locked, _fallback.locked);
                default:
                    return hovered
                        ? Pick(sprites?.hover, _fallback.hover)
                        : Pick(sprites?.available, _fallback.available);
            }
        }

        // 그림에 곱하는 색. Gold가 모자란 노드만 흐리게 한다.
        public Color TintOf(NodeState state) =>
            state == NodeState.Revealed && !_revealedAsLocked ? _revealedTint : Color.white;

        public Color LinkColorOf(NodeState a, NodeState b)
        {
            if (a == NodeState.Owned && b == NodeState.Owned)
                return _ownedLinkColor;

            bool open = (a == NodeState.Owned && b == NodeState.Purchasable) || (b == NodeState.Owned && a == NodeState.Purchasable);
            return open ? _openLinkColor : _lockedLinkColor;
        }

        public Color PriceColorOf(NodeState state) =>
            state == NodeState.Purchasable ? _priceColor : _unaffordablePriceColor;

        private static Sprite Pick(Sprite sprite, Sprite fallback) => sprite != null ? sprite : fallback;

        private NodeSprites Find(string stat)
        {
            if (string.IsNullOrEmpty(stat))
                return null;

            if (_byStat == null)
            {
                _byStat = new Dictionary<string, NodeSprites>(StringComparer.Ordinal);
                foreach (NodeSprites entry in _stats)
                {
                    if (entry != null && !string.IsNullOrEmpty(entry.stat) && !_byStat.ContainsKey(entry.stat))
                        _byStat.Add(entry.stat, entry);
                }
            }

            return _byStat.TryGetValue(stat, out NodeSprites found) ? found : null;
        }

        // 인스펙터에서 목록을 고치면 다시 찾는다.
        private void OnValidate() => _byStat = null;
    }
}
