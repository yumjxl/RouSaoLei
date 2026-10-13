using System;
using GameLogic.LeiPai;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    /// <summary>单张雷牌的纯视图适配器，不执行任何游戏规则。</summary>
    internal sealed class LeiPaiCardView
    {
        private readonly Button _button;
        private readonly TMP_Text _centerLabel;
        private readonly TMP_Text _topClue;
        private readonly TMP_Text _bottomClue;
        private readonly Transform _artwork;

        public Transform Transform => _button.transform;
        public Outline Outline { get; }
        public bool Interactable { set => _button.interactable = value; }

        public LeiPaiCardView(Button button)
        {
            _button = button ?? throw new ArgumentNullException(nameof(button));
            _centerLabel = button.GetComponentInChildren<TMP_Text>();
            _artwork = button.transform.Find("m_go_Artwork");
            if (_artwork != null)
            {
                var labelRect = _centerLabel.rectTransform;
                labelRect.anchorMin = new Vector2(0f, 0f);
                labelRect.anchorMax = new Vector2(1f, .25f);
                labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
                _centerLabel.fontSize = 16f;
                _centerLabel.color = new Color(.23f, .24f, .21f);
            }
            Outline = button.GetComponent<Outline>() ?? button.gameObject.AddComponent<Outline>();
            Outline.effectDistance = new Vector2(1f, -1f);
            Outline.useGraphicAlpha = false;
            _topClue = CreateClueLabel("RuntimeTopClue", TextAlignmentOptions.TopLeft);
            _bottomClue = CreateClueLabel("RuntimeBottomClue", TextAlignmentOptions.BottomRight);
        }

        public void Render(LeiPaiCardState card)
        {
            string centerText;
            Color background;
            if (card.IsRevealed)
            {
                centerText = card.IsClueSplit ? $"{card.Config.Name}\n已拆分" : card.Config.Name;
                background = card.Config.HasEffect("reveal.shield") ? new Color(0.48f, 0.16f, 0.2f) : new Color(0.12f, 0.34f, 0.36f);
                SetClue(FormatClue(card));
            }
            else if (card.IsScouted)
            {
                centerText = $"侦察\n{card.Config.Name}";
                background = new Color(0.19f, 0.3f, 0.48f);
                SetClue(string.Empty);
            }
            else if (card.IsSafeConfirmed)
            {
                centerText = "安全\n基础卡";
                background = new Color(0.17f, 0.42f, 0.3f);
                SetClue(string.Empty);
            }
            else
            {
                centerText = card.Mark == LeiPaiMarkType.None ? (card.IsEntry ? "入口\n?" : "?") : MarkName(card.Mark);
                background = card.IsEntry ? new Color(0.23f, 0.42f, 0.62f) : new Color(0.09f, 0.14f, 0.23f);
                SetClue(string.Empty);
            }

            _button.image.color = background;
            if (_artwork != null)
            {
                _button.image.color = card.IsRevealed ? new Color(1f, .98f, .93f) :
                    card.IsEntry || card.IsSafeConfirmed ? new Color(.89f, .94f, .87f) : new Color(.96f, .94f, .89f);
                string icon = card.IsRevealed || card.IsScouted ? card.Config.Icon : "card_back";
                bool found = _artwork.Find(icon) != null;
                foreach (Transform child in _artwork) child.gameObject.SetActive(child.name == (found ? icon : "card_back"));
                if (!card.IsRevealed && !card.IsScouted && card.Mark == LeiPaiMarkType.None && !card.IsSafeConfirmed)
                    centerText = card.IsEntry ? "从这里开始" : string.Empty;
            }
            Outline.effectColor = GetStateBorder(card);
            _centerLabel.text = centerText;
        }

        private TMP_Text CreateClueLabel(string name, TextAlignmentOptions alignment)
        {
            GameObject node = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rect = node.GetComponent<RectTransform>();
            rect.SetParent(_button.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(8f, 6f);
            rect.offsetMax = new Vector2(-8f, -6f);

            TMP_Text clue = node.GetComponent<TMP_Text>();
            clue.font = _centerLabel.font;
            clue.fontSharedMaterial = _centerLabel.fontSharedMaterial;
            clue.fontSize = 17f;
            clue.fontStyle = FontStyles.Bold;
            clue.alignment = alignment;
            clue.enableWordWrapping = false;
            clue.richText = true;
            clue.raycastTarget = false;
            return clue;
        }

        private void SetClue(string text)
        {
            _topClue.text = text;
            _bottomClue.text = text;
        }

        private static string FormatClue(LeiPaiCardState card)
        {
            int mines = card.Clue.MineCount;
            int cores = card.Clue.CoreCount;
            if (card.IsClueSplit) return $"<color=#FF5C6C>雷 {mines}</color> / <color=#62E69B>核 {cores}</color>";
            if (mines > 0 && cores > 0) return $"<color=#AF7B18>{mines + cores}</color>";
            if (mines > 0) return $"<color=#C34F46>{mines}</color>";
            if (cores > 0) return $"<color=#4F7759>{cores}</color>";
            return "<color=#8993A8>· 0</color>";
        }

        private static Color GetStateBorder(LeiPaiCardState card)
        {
            if (card.IsRevealed || card.IsScouted)
            {
                if (string.Equals(card.Config.ClueRole, "mine", StringComparison.OrdinalIgnoreCase)) return new Color(1f, 0.25f, 0.32f, 1f);
                if (string.Equals(card.Config.Category, "core", StringComparison.OrdinalIgnoreCase)) return new Color(1f, 0.73f, 0.22f, 1f);
                if (string.Equals(card.Config.Category, "tool", StringComparison.OrdinalIgnoreCase)) return new Color(0.28f, 0.77f, 1f, 1f);
                return new Color(0.48f, 0.58f, 0.72f, 0.8f);
            }
            if (card.IsSafeConfirmed) return new Color(0.3f, 0.95f, 0.58f, 1f);
            if (card.Mark == LeiPaiMarkType.SuspectedMine) return new Color(1f, 0.25f, 0.32f, 1f);
            if (card.Mark == LeiPaiMarkType.SuspectedCore) return new Color(1f, 0.73f, 0.22f, 1f);
            if (card.IsEntry) return new Color(0.38f, 0.72f, 1f, 0.9f);
            return new Color(0.18f, 0.28f, 0.43f, 0.75f);
        }

        private static string MarkName(LeiPaiMarkType mark)
            => mark == LeiPaiMarkType.SuspectedMine ? "疑似地雷" : "疑似核心";
    }
}
