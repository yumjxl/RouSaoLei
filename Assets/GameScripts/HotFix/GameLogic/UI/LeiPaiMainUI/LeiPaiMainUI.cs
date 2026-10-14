using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameLogic.LeiPai;
using SuperScrollView;
using TMPro;
using TEngine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameLogic
{
    [Window(UILayer.UI, location: "LeiPaiMainUI")]
    internal sealed class LeiPaiMainUI : UIWindow
    {
        private readonly Button[] _rewards = new Button[3];
        private readonly LeiPaiTutorialFlow _tutorial = new LeiPaiTutorialFlow();
        private TMP_Text _stage;
        private TMP_Text _score;
        private TMP_Text _turn;
        private TMP_Text _shield;
        private TMP_Text _scout;
        private TMP_Text _remain;
        private TMP_Text _charge;
        private TMP_Text _core;
        private TMP_Text _log;
        private TMP_Text _lastTotal;
        private TMP_Text _hint;
        private RectTransform _scoreProgressFill;
        private LoopGridView _boardGrid;
        private Button _scoutMode;
        private Button _retry;
        private GameObject _rewardPanel;
        private LeiPaiRunRules _runRules;
        private LeiPaiRunState _run;
        private ILeiPaiCardCatalog _catalog;
        private bool _isScoutMode;
        private bool _inputLocked;
        private CancellationTokenSource _animationCts;
        private IReadOnlyList<int> _rewardChoices = Array.Empty<int>();

        protected override void ScriptGenerator()
        {
            _stage = FindChildComponent<TMP_Text>("m_go_TopBar/m_go_StageStat/m_tmp_Stage");
            _score = FindChildComponent<TMP_Text>("m_go_TopBar/m_go_ScoreStat/m_tmp_Score");
            _turn = FindChildComponent<TMP_Text>("m_go_TopBar/m_go_TurnStat/m_tmp_Turn");
            _shield = FindChildComponent<TMP_Text>("m_go_TopBar/m_go_ShieldStat/m_tmp_Shield");
            _scout = FindChildComponent<TMP_Text>("m_go_TopBar/m_go_ScoutStat/m_tmp_Scout");
            _remain = FindChildComponent<TMP_Text>("m_go_SidePanel/m_go_DeckInfo/m_go_RemainRow/m_tmp_Remain");
            _charge = FindChildComponent<TMP_Text>("m_go_SidePanel/m_go_DeckInfo/m_go_ChargeRow/m_tmp_Charge");
            _core = FindChildComponent<TMP_Text>("m_go_SidePanel/m_go_DeckInfo/m_go_CoreRow/m_tmp_Core");
            _log = FindChildComponent<TMP_Text>("m_go_SidePanel/m_go_LogSection/m_tmp_Log");
            _lastTotal = FindChildComponent<TMP_Text>("m_go_SidePanel/m_go_LogSection/m_tmp_LastTotal");
            _hint = FindChildComponent<TMP_Text>("m_go_BoardPanel/m_go_BoardHeader/m_tmp_Hint");
            _scoreProgressFill = FindChildComponent<RectTransform>("m_go_TopBar/m_go_ScoreStat/m_go_ScoreProgress/m_go_ScoreProgressFill");
            _scoutMode = FindChildComponent<Button>("m_go_SidePanel/m_btn_ScoutMode");
            _retry = FindChildComponent<Button>("m_go_SidePanel/m_go_TipSection/m_btn_Retry");
            _rewardPanel = FindChild("m_go_RewardPanel").gameObject;
            _boardGrid = FindChildComponent<LoopGridView>("m_go_BoardPanel/m_grid_Board");
            for (int i = 0; i < _rewards.Length; i++)
                _rewards[i] = FindChildComponent<Button>($"m_go_RewardPanel/m_go_RewardBox/m_go_RewardCards/m_btn_Reward{i}");
        }

        protected override void RegisterEvent()
        {
            _scoutMode.onClick.AddListener(ToggleScoutMode);
            _retry.onClick.AddListener(Retry);
            for (int i = 0; i < _rewards.Length; i++)
            {
                int index = i;
                _rewards[i].onClick.AddListener(() => SelectReward(index));
            }
        }

        protected override void OnCreate()
        {
            // Recover old window assets saved with zero scale by a Canvas preview scene.
            rectTransform.localScale = Vector3.one;
            Canvas.ForceUpdateCanvases();
            _animationCts = new CancellationTokenSource();
            ConfigSystem.Instance.Load();
            _catalog = LeiPaiLubanCardCatalog.Load();
            _runRules = new LeiPaiRunRules(LeiPaiLubanLevelCatalog.Load(), _catalog);
            _tutorial.Begin();
            _run = _runRules.StartNewRun(LeiPaiTutorialFlow.BoardSeed);
            _boardGrid.InitGridView(_run.CurrentGame.Board.Cards.Count, OnGetCardItem, CreateGridSettings(_run.CurrentGame));
            _rewardPanel.SetActive(false);
            SetButtonText(_retry, _tutorial.PrimaryButtonText);
            _log.text = _tutorial.Prompt();
            RefreshView();
            UpdateModeHint();
        }

        protected override void OnDestroy()
        {
            _animationCts?.Cancel();
            _animationCts?.Dispose();
            _animationCts = null;
            _scoutMode?.onClick.RemoveAllListeners();
            _retry?.onClick.RemoveAllListeners();
            foreach (Button reward in _rewards) reward?.onClick.RemoveAllListeners();
        }

        private LoopGridViewItem OnGetCardItem(LoopGridView grid, int itemIndex, int row, int column)
        {
            if (_run?.CurrentGame == null || itemIndex < 0 || itemIndex >= _run.CurrentGame.Board.Cards.Count) return null;
            LoopGridViewItem item = grid.NewListViewItem("LeiPaiCardItem");
            if (item == null) return null;

            LeiPaiCardView view = item.UserObjectData as LeiPaiCardView;
            if (view == null)
            {
                Button button = item.GetComponent<Button>();
                view = new LeiPaiCardView(button);
                item.UserObjectData = view;

                EventTrigger trigger = item.gameObject.GetComponent<EventTrigger>() ?? item.gameObject.AddComponent<EventTrigger>();
                EventTrigger.Entry click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
                click.callback.AddListener(data => OnCardPointer(item.ItemIndex, (PointerEventData)data));
                trigger.triggers.Add(click);
                EventTrigger.Entry enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enter.callback.AddListener(_ => ShowCardHint(item.ItemIndex));
                trigger.triggers.Add(enter);
                EventTrigger.Entry exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                exit.callback.AddListener(_ => UpdateModeHint());
                trigger.triggers.Add(exit);
            }

            view.Render(_run.CurrentGame.Board.Cards[itemIndex]);
            view.Interactable = !_inputLocked;
            return item;
        }

        private LoopGridViewSettingParam CreateGridSettings(LeiPaiGameState game)
        {
            RectTransform boardRect = _boardGrid.transform as RectTransform;
            GameObject itemPrefab = _boardGrid.ItemPrefabDataList.FirstOrDefault()?.mItemPrefab;
            RectTransform itemRect = itemPrefab == null ? null : itemPrefab.transform as RectTransform;
            if (boardRect == null || itemRect == null) throw new InvalidOperationException("雷牌超级列表缺少棋盘尺寸或 Item Prefab 配置。");

            Vector2 boardSize = boardRect.rect.size;
            Vector2 itemSize = itemRect.rect.size;
            if (itemSize.x <= 0f || itemSize.y <= 0f) itemSize = itemRect.sizeDelta;
            int columns = game.Board.Width;
            int rows = Mathf.CeilToInt(game.Board.Cards.Count / (float)columns);
            float horizontalGap = columns > 1 ? Mathf.Max(0f, Mathf.Round((boardSize.x - itemSize.x * columns) / (columns - 1))) : 0f;
            float verticalGap = rows > 1 ? Mathf.Max(0f, Mathf.Round((boardSize.y - itemSize.y * rows) / (rows - 1))) : 0f;

            return new LoopGridViewSettingParam
            {
                mItemSize = itemSize,
                mItemPadding = new Vector2(horizontalGap, verticalGap),
                mPadding = new RectOffset(0, 0, 0, 0),
                mGridFixedType = GridFixedType.ColumnCountFixed,
                mFixedRowOrColumnCount = columns
            };
        }

        private LeiPaiCardView GetShownCardView(int index)
            => _boardGrid.GetShownItemByItemIndex(index)?.UserObjectData as LeiPaiCardView;

        private void OnCardPointer(int index, PointerEventData data)
        {
            if (_inputLocked) return;
            LeiPaiGameState game = _run.CurrentGame;
            LeiPaiPosition position = new LeiPaiPosition(index % game.Board.Width, index / game.Board.Width);
            if (data.button == PointerEventData.InputButton.Right)
            {
                if (!_tutorial.AllowsMark)
                {
                    _log.text = _tutorial.Prompt();
                    return;
                }
                LeiPaiMarkType mark = new LeiPaiGameRules(_catalog).ToggleMark(game, position);
                PlayCue(LeiPaiUiCue.Mark);
                _log.text = mark == LeiPaiMarkType.None ? "已取消标记。" : $"标记为：{MarkName(mark)}";
                RefreshView();
                if (_tutorial.NotifyMarked(mark)) _log.text += "\n\n" + _tutorial.Prompt();
                return;
            }

            if (!_tutorial.AllowsLeftClick(_isScoutMode))
            {
                _log.text = _tutorial.Prompt();
                return;
            }

            ResolveCardAsync(index, game, position).Forget();
        }

        private async UniTaskVoid ResolveCardAsync(int index, LeiPaiGameState game, LeiPaiPosition position)
        {
            CancellationToken token = _animationCts.Token;
            int scoreBefore = game.Score;

            LeiPaiActionResult result;
            LeiPaiGameRules rules = new LeiPaiGameRules(_catalog);
            LeiPaiCardState card = game.Board.GetCard(position);
            if (_isScoutMode)
                result = card.IsRevealed ? rules.SplitClue(game, position) : rules.ScoutCard(game, position);
            else
                result = rules.Reveal(game, position);

            if (result.Status != LeiPaiActionStatus.Success)
            {
                _log.text = Describe(result);
                return;
            }

            _inputLocked = true;
            SetBoardInteractable(false);
            LeiPaiCardView cardView = GetShownCardView(index);
            try
            {
                PlayCue(_isScoutMode ? LeiPaiUiCue.Scout : LeiPaiUiCue.Flip);
                if (cardView != null) await PlayCardFlip(cardView.Transform, token);
                cardView?.Render(result.Card);
                if (cardView != null) await PlayCardUnfold(cardView.Transform, token);

                if (result.Contributions.Count == 0)
                {
                    if (result.Card.IsCore()) PlayStyle(LeiPaiPresentationProfile.Get(LeiPaiContributionKind.Core));
                    _log.text = $"{result.Card?.Config.Name ?? "侦察"}：操作成功。";
                    _lastTotal.text = "总计　0";
                    await UniTask.Delay(180, cancellationToken: token);
                }
                else
                {
                    int displayScore = scoreBefore;
                    List<string> lines = new List<string>();
                    foreach (LeiPaiScoreContribution contribution in result.Contributions)
                    {
                        PlayStyle(LeiPaiPresentationProfile.Get(contribution.Kind));
                        displayScore = Math.Max(0, displayScore + contribution.Value);
                        lines.Add($"{contribution.Source} {(contribution.Value >= 0 ? "+" : string.Empty)}{contribution.Value}");
                        _log.text = string.Join("\n", lines);
                        _score.text = $"{displayScore} / {game.Config.TargetScore}";
                        int sourceIndex = FindContributionCardIndex(game.Board.Width, contribution, index);
                        await PulseContribution(sourceIndex, contribution, token);
                        await UniTask.Delay(140, cancellationToken: token);
                    }
                    _lastTotal.text = $"总计　{(result.ScoreDelta >= 0 ? "+" : string.Empty)}{result.ScoreDelta}";
                }

                RefreshView();
                AdvanceTutorialAfterAction(result);
                if (!_tutorial.IsActive && game.IsWon) ShowRewardsOrComplete();
                else if (_tutorial.IsActive && (game.IsWon || game.IsLost))
                {
                    _tutorial.NotifyBoardEnded();
                    SetButtonText(_retry, _tutorial.PrimaryButtonText);
                    _log.text += "\n教学牌局已结束，点击“开始远征”进入正式随机三关。";
                }
                else if (game.IsLost) _log.text += "\n牌局失败，可点击重试当前牌局。";
            }
            catch (OperationCanceledException)
            {
                // 窗口销毁时取消表现队列，不回写任何规则状态。
            }
            finally
            {
                if (cardView != null) cardView.Transform.localScale = Vector3.one;
                _inputLocked = false;
                SetBoardInteractable(true);
            }
        }

        private void ToggleScoutMode()
        {
            if (_inputLocked) return;
            if (!_tutorial.AllowsScoutToggle)
            {
                _log.text = _tutorial.Prompt();
                return;
            }
            _isScoutMode = !_isScoutMode;
            PlayCue(LeiPaiUiCue.Scout);
            SetButtonText(_scoutMode, _isScoutMode ? "关闭侦察" : $"侦察 { _run.CurrentGame.RemainingScouts } 次");
            _log.text = _isScoutMode ? "侦察模式：点击未翻卡查看身份；点击混合线索可拆分。" : "已返回翻牌模式。";
            UpdateModeHint();
        }

        private void Retry()
        {
            if (_inputLocked) return;
            if (_tutorial.IsActive)
            {
                StartAdventure();
                return;
            }
            if (_run.AwaitingReward || _run.IsComplete) return;
            _runRules.RetryCurrentBoard(_run);
            _isScoutMode = false;
            SetButtonText(_scoutMode, $"侦察 { _run.CurrentGame.RemainingScouts } 次");
            _log.text = "已按相同种子重置，卡牌布局保持不变。";
            RefreshView();
            UpdateModeHint();
        }

        private void StartAdventure()
        {
            _tutorial.Leave();
            _isScoutMode = false;
            _rewardChoices = Array.Empty<int>();
            _run = _runRules.StartNewRun(Environment.TickCount);
            _rewardPanel.SetActive(false);
            SetButtonText(_scoutMode, $"侦察 { _run.CurrentGame.RemainingScouts } 次");
            SetButtonText(_retry, "重试当前牌局");
            _log.text = "随机三关远征开始。利用线索、标记和侦察，在回合耗尽前达到目标分。";
            RefreshView();
            UpdateModeHint();
        }

        private void ShowRewardsOrComplete()
        {
            PlayCue(LeiPaiUiCue.Goal);
            _rewardChoices = _runRules.CompleteStage(_run);
            if (_run.IsComplete)
            {
                _log.text = "三关全部完成，本轮雷牌挑战胜利！";
                return;
            }
            _rewardPanel.SetActive(true);
            for (int i = 0; i < _rewards.Length; i++)
            {
                LeiPaiCardConfig config = _catalog.Get(_rewardChoices[i]);
                SetButtonText(_rewards[i], $"{config.Name}\n\n{config.Description}");
            }
        }

        private void SelectReward(int index)
        {
            if (_inputLocked || !_run.AwaitingReward || index >= _rewardChoices.Count) return;
            _runRules.SelectReward(_run, _rewardChoices[index]);
            PlayStyle(LeiPaiPresentationProfile.Get(LeiPaiContributionKind.Core));
            _rewardPanel.SetActive(false);
            _log.text = "奖励已加入核心池，下一关开始。";
            RefreshView();
            UpdateModeHint();
        }

        private void RefreshView()
        {
            LeiPaiGameState game = _run.CurrentGame;
            if (game == null) return;
            _stage.text = $"{_run.CurrentStageId} / 3";
            _score.text = $"{game.Score} / {game.Config.TargetScore}";
            _turn.text = game.RemainingTurns.ToString();
            _shield.text = game.Shield.ToString();
            _scout.text = game.RemainingScouts.ToString();
            _remain.text = game.ActiveCores.Count == 0 ? "等待激活" : game.ActiveCores[0].Card.Config.Name;
            _charge.text = $"蓄能 {game.ChargedScore}";
            _core.text = $"核心 {game.ActiveCores.Count}";
            SetButtonText(_scoutMode, _isScoutMode ? "关闭侦察" : $"侦察 {game.RemainingScouts} 次");
            if (_scoreProgressFill != null)
            {
                float progress = game.Config.TargetScore <= 0 ? 0f : Mathf.Clamp01(game.Score / (float)game.Config.TargetScore);
                _scoreProgressFill.pivot = new Vector2(0f, 0.5f);
                _scoreProgressFill.localScale = new Vector3(progress, 1f, 1f);
            }
            _boardGrid.RefreshAllShownItem();
        }

        private static string FormatRemainingComposition(LeiPaiGameState game)
        {
            IEnumerable<LeiPaiCardState> hidden = game.Board.Cards.Where(card => !card.IsRevealed);
            int basic = hidden.Count(card => string.Equals(card.Config.Category, "normal", StringComparison.OrdinalIgnoreCase));
            int mines = hidden.Count(card => string.Equals(card.Config.ClueRole, "mine", StringComparison.OrdinalIgnoreCase));
            int cores = hidden.Count(card => string.Equals(card.Config.Category, "core", StringComparison.OrdinalIgnoreCase));
            int tools = hidden.Count(card => string.Equals(card.Config.Category, "tool", StringComparison.OrdinalIgnoreCase));
            return $"基{basic} 雷{mines} 核{cores} 工{tools}";
        }

        private static string Describe(LeiPaiActionResult result)
        {
            if (result.Status != LeiPaiActionStatus.Success) return $"操作未执行：{result.Status}";
            if (result.Contributions.Count == 0) return $"{result.Card?.Config.Name ?? "侦察"}：操作成功。";
            return string.Join("\n", result.Contributions.Select(item => $"{item.Source} {(item.Value >= 0 ? "+" : string.Empty)}{item.Value}"));
        }

        private static string MarkName(LeiPaiMarkType mark)
            => mark == LeiPaiMarkType.SuspectedMine ? "疑似地雷" : "疑似核心";

        private void SetBoardInteractable(bool interactable)
        {
            for (int i = 0; i < _run.CurrentGame.Board.Cards.Count; i++)
            {
                LeiPaiCardView card = GetShownCardView(i);
                if (card != null) card.Interactable = interactable;
            }
            _scoutMode.interactable = interactable;
            _retry.interactable = interactable;
            if (!interactable && _hint != null) _hint.text = "结算中，请等待联动播放完成……";
            else UpdateModeHint();
        }

        private void ShowCardHint(int index)
        {
            if (_hint == null || _inputLocked || _run?.CurrentGame == null) return;
            if (index < 0 || index >= _run.CurrentGame.Board.Cards.Count) return;
            LeiPaiCardState card = _run.CurrentGame.Board.Cards[index];
            if (card.IsRevealed || card.IsScouted)
            {
                string description = string.IsNullOrWhiteSpace(card.Config.Description) ? "暂无额外规则说明。" : card.Config.Description;
                _hint.text = $"{card.Config.Name}：{description}";
                return;
            }

            if (card.IsSafeConfirmed) _hint.text = "安全确认：系统已确认这是一张基础卡。";
            else if (card.Mark != LeiPaiMarkType.None) _hint.text = $"玩家笔记：{MarkName(card.Mark)}，不代表系统确认。";
            else _hint.text = _isScoutMode ? "未知卡牌：点击消耗一次侦察并查看类型。" : "未知卡牌：左键翻开，右键添加玩家标记。";
        }

        private void UpdateModeHint()
        {
            if (_hint == null || _inputLocked) return;
            _hint.text = _isScoutMode
                ? "侦察模式 · 未翻卡查类型 · 已翻黄色线索可拆分"
                : "翻牌模式 · 左键翻牌 · 右键切换标记 · 悬停查看已知规则";
        }

        private void AdvanceTutorialAfterAction(LeiPaiActionResult result)
        {
            if (!_tutorial.IsActive) return;
            int previousStep = _tutorial.Step;
            if (!_tutorial.NotifyAction(result, _isScoutMode)) return;
            if (previousStep == 2 && _tutorial.Step == 3)
            {
                _isScoutMode = false;
                SetButtonText(_scoutMode, $"侦察 {_run.CurrentGame.RemainingScouts} 次");
            }
            SetButtonText(_retry, _tutorial.PrimaryButtonText);
            _log.text += "\n\n" + _tutorial.Prompt();
        }

        private static void PlayCue(LeiPaiUiCue cue)
        {
            PlayStyle(LeiPaiPresentationProfile.Get(cue));
        }

        private static void PlayStyle(LeiPaiPresentationStyle style)
        {
            GameModule.Audio.Play(TEngine.AudioType.UISound, style.SoundAddress, volume: style.Volume, bAsync: true, bInPool: true);
        }

        private static int FindContributionCardIndex(int boardWidth, LeiPaiScoreContribution contribution, int revealedIndex)
        {
            if (!contribution.SourcePosition.HasValue) return revealedIndex;
            LeiPaiPosition position = contribution.SourcePosition.Value;
            return position.Y * boardWidth + position.X;
        }

        private async UniTask PulseContribution(int index, LeiPaiScoreContribution contribution, CancellationToken token)
        {
            LeiPaiCardView view = GetShownCardView(index);
            if (view == null) return;
            Outline outline = view.Outline;
            Color previous = outline.effectColor;
            outline.effectColor = LeiPaiPresentationProfile.Get(contribution.Kind).HighlightColor;
            await Pulse(view.Transform, token);
            outline.effectColor = previous;
        }

        private static async UniTask PlayCardFlip(Transform card, CancellationToken token)
        {
            for (int i = 5; i >= 0; i--)
            {
                card.localScale = new Vector3(i / 5f, 1f, 1f);
                await UniTask.Delay(20, cancellationToken: token);
            }
        }

        private static async UniTask PlayCardUnfold(Transform card, CancellationToken token)
        {
            for (int i = 0; i <= 5; i++)
            {
                card.localScale = new Vector3(i / 5f, 1f, 1f);
                await UniTask.Delay(20, cancellationToken: token);
            }
        }

        private static async UniTask Pulse(Transform target, CancellationToken token)
        {
            target.localScale = Vector3.one * 1.06f;
            await UniTask.Delay(55, cancellationToken: token);
            target.localScale = Vector3.one;
        }

        private static void SetButtonText(Button button, string text)
        {
            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = text;
        }
    }
}
