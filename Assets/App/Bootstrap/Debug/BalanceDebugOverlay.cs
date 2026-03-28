using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer;
using UnityEngine.InputSystem;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Domain.Timing;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Player.Domain;
using FloorBreaker.Slimes.Domain;
using FloorBreaker.Bombs.Domain;
using FloorBreaker.Upgrades.Application;
using FloorBreaker.MatchFlow.Application;

namespace FloorBreaker.Bootstrap
{
    /// <summary>
    /// バランス調整用 IMGUI デバッグオーバーレイ。
    /// MatchLifetimeScope の autoInjectGameObjects に登録し、
    /// 同一コンテナから [Inject] で Scoped インスタンスを共有する。
    /// </summary>
    public sealed class BalanceDebugOverlay : MonoBehaviour
    {
        // --- Injected ---
        private MatchPlayers _players;
        private MatchClock _clock;
        private MatchPhaseScheduler _scheduler;
        private StageModel _stage;
        private SlimeRegistry _slimeRegistry;
        private SlimeSpawnService _slimeSpawnService;
        private StageShrinkService _shrinkService;
        private UpgradeApplyService _upgradeApply;
        private IBalanceParameters _balance;

        // --- UI State ---
        private bool _visible;
        private int _selectedTab;
        private Vector2 _scrollPos;
        private Rect _windowRect;
        private int _upgradeIndexP1;
        private int _upgradeIndexP2;
        private bool _initialized;

        // --- Cached ---
        private string[] _upgradeNames;
        private UpgradeId[] _upgradeValues;
        private readonly string[] _tabLabels = { "Overview", "P1", "P2", "Cheats", "Time" };
        private int _initialStageSize;

        [Inject]
        public void Construct(
            MatchPlayers players,
            MatchClock clock,
            MatchPhaseScheduler scheduler,
            StageModel stage,
            SlimeRegistry slimeRegistry,
            SlimeSpawnService slimeSpawnService,
            StageShrinkService shrinkService,
            UpgradeApplyService upgradeApply,
            IBalanceParameters balance)
        {
            _players = players;
            _clock = clock;
            _scheduler = scheduler;
            _stage = stage;
            _slimeRegistry = slimeRegistry;
            _slimeSpawnService = slimeSpawnService;
            _shrinkService = shrinkService;
            _upgradeApply = upgradeApply;
            _balance = balance;

            _upgradeValues = Enum.GetValues(typeof(UpgradeId))
                .Cast<UpgradeId>()
                .Where(id => id != UpgradeId.None)
                .ToArray();
            _upgradeNames = _upgradeValues.Select(id => id.ToString()).ToArray();

            _initialStageSize = balance.StageSize;
            _windowRect = new Rect(Screen.width - 370, 10, 360, Screen.height - 20);
            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized) return;

            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.f1Key.wasPressedThisFrame) _visible = !_visible;
            if (kb.f2Key.wasPressedThisFrame) TogglePause();
            if (kb.f3Key.wasPressedThisFrame) SkipToNextPhase();
            if (kb.f4Key.wasPressedThisFrame) _slimeSpawnService.SpawnIfNeeded();
            if (kb.f5Key.wasPressedThisFrame) RestartScene();
            if (kb.f9Key.wasPressedThisFrame) PresetLateGame();
            if (kb.f10Key.wasPressedThisFrame) PresetMaxBuild();
        }

        private void OnGUI()
        {
            if (!_initialized || !_visible) return;
            _windowRect = GUILayout.Window(91827, _windowRect, DrawWindow, "Balance Debug");
        }

        // ================================================================
        // Window
        // ================================================================

        private void DrawWindow(int id)
        {
            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabLabels);
            _scrollPos = GUILayout.BeginScrollView(_scrollPos);

            switch (_selectedTab)
            {
                case 0: DrawOverview(); break;
                case 1: DrawPlayerDetail(_players.Player1, _players.Cooldown1, ref _upgradeIndexP1); break;
                case 2: DrawPlayerDetail(_players.Player2, _players.Cooldown2, ref _upgradeIndexP2); break;
                case 3: DrawCheats(); break;
                case 4: DrawTimeControl(); break;
            }

            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        // ================================================================
        // Tab 0: Overview
        // ================================================================

        private void DrawOverview()
        {
            Section("MATCH");
            Label($"Phase: {_clock.CurrentPhaseValue}  ({_scheduler.State})");
            Label($"Timer: {_clock.RemainingValue:F1}s / {_clock.PhaseDuration:F0}s");
            Label($"Paused: {_clock.IsPausedValue}");

            Section("STAGE");
            var bounds = _stage.Bounds.Current;
            int alive = _stage.GetAliveTileCount();
            int total = _initialStageSize * _initialStageSize;
            int shrinks = (_initialStageSize - bounds.Width) / 2;
            Label($"Tiles: {alive} / {total}  Bounds: ({bounds.MinX},{bounds.MinY})-({bounds.MaxX},{bounds.MaxY})  Shrinks: {shrinks}");

            Section("SLIMES");
            int target = (int)(alive * _balance.SlimeTargetRatio + 0.001f);
            Label($"Alive: {_slimeRegistry.AliveCount}  Target: {target}");

            Section("PLAYERS");
            var p1 = _players.Player1;
            var p2 = _players.Player2;
            GUILayout.BeginHorizontal();
            GUILayout.Label("", GUILayout.Width(80));
            GUILayout.Label("P1", BoldLabel(), GUILayout.Width(100));
            GUILayout.Label("P2", BoldLabel(), GUILayout.Width(100));
            GUILayout.EndHorizontal();

            CompareRow("HP", $"{p1.Stats.CurrentHp.CurrentValue}/{p1.Stats.MaxHp}", $"{p2.Stats.CurrentHp.CurrentValue}/{p2.Stats.MaxHp}");
            CompareRow("Coins", $"{p1.Stats.Coins.CurrentValue}", $"{p2.Stats.Coins.CurrentValue}");
            CompareRow("Pos", $"({p1.CurrentPosition.X},{p1.CurrentPosition.Y})", $"({p2.CurrentPosition.X},{p2.CurrentPosition.Y})");
            CompareRow("Speed", $"{p1.Stats.MoveSpeed:F1}", $"{p2.Stats.MoveSpeed:F1}");

            var u1 = p1.Build.AcquiredUpgrades.CurrentValue;
            var u2 = p2.Build.AcquiredUpgrades.CurrentValue;
            CompareRow("Upgrades", $"{u1.Count}", $"{u2.Count}");
        }

        // ================================================================
        // Tab 1/2: Player Detail
        // ================================================================

        private void DrawPlayerDetail(PlayerModel player, BombCooldownState cooldown, ref int upgradeIndex)
        {
            var s = player.Stats;
            var b = player.Build;

            // --- Status ---
            Section($"PLAYER {player.Id.Index + 1}  STATUS");
            GUILayout.BeginHorizontal();
            Label($"HP: {s.CurrentHp.CurrentValue}/{s.MaxHp}");
            if (Btn("+1")) s.Heal(1);
            if (Btn("-1")) s.TakeDamage(1);
            if (Btn("Max")) s.Heal(s.MaxHp);
            if (Btn("Kill")) s.TakeDamage(s.MaxHp + 1);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Label($"Coins: {s.Coins.CurrentValue}");
            if (Btn("+5")) s.AddCoins(5);
            if (Btn("+10")) s.AddCoins(10);
            if (Btn("+50")) s.AddCoins(50);
            GUILayout.EndHorizontal();

            Label($"Pos: ({player.CurrentPosition.X},{player.CurrentPosition.Y})  Facing: {player.CurrentFacing}");
            Label($"MoveSpeed: {s.MoveSpeed:F1} / {s.MaxMoveSpeed:F1}");

            // Invulnerability
            GUILayout.BeginHorizontal();
            Label($"Invulnerable: {player.Invulnerability.IsInvulnerable} ({player.Invulnerability.RemainingDuration:F1}s)");
            if (Btn("+3s")) player.Invulnerability.Activate(3f);
            GUILayout.EndHorizontal();

            // --- Fire Bomb ---
            Section("FIRE BOMB");
            Label($"Flight: {b.FireFlightRange}  Effect: {b.FireEffectRange}  Dmg: {b.FireDamage}");
            Label($"CD: {b.FireCooldown:F1}s (min {b.FireCooldownMin:F1})  Duration: {b.FireDuration:F1}s");
            Label($"WallPen: {b.FireWallPenetration}  BombPen: {b.HasFireBombPenetration}");
            GUILayout.BeginHorizontal();
            Label($"CD Remaining: {cooldown.FireBombRemaining.CurrentValue:F1}s");
            if (Btn("Reset CD")) cooldown.StartCooldown(BombType.Fire, 0f);
            GUILayout.EndHorizontal();

            // --- Break Bomb ---
            Section("BREAK BOMB");
            Label($"Flight: {b.BreakFlightRange}  Effect: {b.BreakEffectRange}  Dmg: {b.BreakDamage}");
            Label($"CD: {b.BreakCooldown:F1}s (min {b.BreakCooldownMin:F1})  Collapse: {b.BreakCollapseTime:F1}s");
            Label($"BombPen: {b.HasBreakBombPenetration}");
            GUILayout.BeginHorizontal();
            Label($"CD Remaining: {cooldown.BreakBombRemaining.CurrentValue:F1}s");
            if (Btn("Reset CD")) cooldown.StartCooldown(BombType.Break, 0f);
            GUILayout.EndHorizontal();

            // --- Abilities ---
            Section("ABILITIES");
            Label($"Dash: {b.HasDash}  DualShot: {b.HasDualShot}");
            Label($"FireShield: {s.FireShieldActive.CurrentValue}  Levitation: {s.LevitationActive.CurrentValue}");

            // --- Acquired Upgrades ---
            Section("ACQUIRED UPGRADES");
            var acquired = b.AcquiredUpgrades.CurrentValue;
            if (acquired.Count == 0)
            {
                Label("(none)");
            }
            else
            {
                Label(string.Join(", ", acquired));
            }

            // --- Apply Upgrade ---
            Section("APPLY UPGRADE");
            GUILayout.BeginHorizontal();
            upgradeIndex = GUILayout.SelectionGrid(
                upgradeIndex, _upgradeNames, 3, GUILayout.MaxWidth(340));
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Apply Selected"))
            {
                var id = _upgradeValues[upgradeIndex];
                Debug.Log($"[BalanceDebug] Applying {id} to P{player.Id.Index + 1}");
                _upgradeApply.Apply(id, player);
            }
        }

        // ================================================================
        // Tab 3: Cheats
        // ================================================================

        private void DrawCheats()
        {
            Section("QUICK UPGRADES");
            GUILayout.BeginHorizontal();
            if (Btn("All Fire (P1)")) ApplyAllCategory(_players.Player1, "Fire");
            if (Btn("All Fire (P2)")) ApplyAllCategory(_players.Player2, "Fire");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (Btn("All Break (P1)")) ApplyAllCategory(_players.Player1, "Break");
            if (Btn("All Break (P2)")) ApplyAllCategory(_players.Player2, "Break");
            GUILayout.EndHorizontal();

            Section("ECONOMY");
            GUILayout.BeginHorizontal();
            if (Btn("+10 coins both")) { _players.Player1.Stats.AddCoins(10); _players.Player2.Stats.AddCoins(10); }
            if (Btn("+50 coins both")) { _players.Player1.Stats.AddCoins(50); _players.Player2.Stats.AddCoins(50); }
            GUILayout.EndHorizontal();

            Section("SLIMES");
            GUILayout.BeginHorizontal();
            if (Btn("Force Spawn")) _slimeSpawnService.SpawnIfNeeded();
            if (Btn("Kill All")) KillAllSlimes();
            GUILayout.EndHorizontal();

            Section("STAGE");
            GUILayout.BeginHorizontal();
            if (Btn("Shrink x1")) _shrinkService.ShrinkOuterRing(_stage);
            if (Btn("Shrink x3")) { for (int i = 0; i < 3; i++) _shrinkService.ShrinkOuterRing(_stage); }
            if (Btn("Shrink to 10x10")) ShrinkTo(10);
            GUILayout.EndHorizontal();

            Section("DAMAGE TEST");
            GUILayout.BeginHorizontal();
            if (Btn("P1 -3")) _players.Player1.Stats.TakeDamage(3);
            if (Btn("P2 -3")) _players.Player2.Stats.TakeDamage(3);
            if (Btn("P1 -5")) _players.Player1.Stats.TakeDamage(5);
            if (Btn("P2 -5")) _players.Player2.Stats.TakeDamage(5);
            GUILayout.EndHorizontal();

            Section("PRESETS");
            GUILayout.BeginHorizontal();
            if (Btn("Late Game")) PresetLateGame();
            if (Btn("Max Build")) PresetMaxBuild();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (Btn("Low HP")) PresetLowHp();
            if (Btn("Economy Test")) PresetEconomy();
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            if (GUILayout.Button("RESTART SCENE", GUILayout.Height(30)))
                RestartScene();
        }

        // ================================================================
        // Tab 4: Time Control
        // ================================================================

        private void DrawTimeControl()
        {
            Section("PHASE TIMER");
            Label($"Phase: {_clock.CurrentPhaseValue}  Timer: {_clock.RemainingValue:F1}s / {_clock.PhaseDuration:F0}s");
            Label($"Paused: {_clock.IsPausedValue}");

            GUILayout.BeginHorizontal();
            if (Btn("Pause")) _clock.Pause();
            if (Btn("Resume")) _clock.Resume();
            GUILayout.EndHorizontal();

            Section("SKIP");
            if (GUILayout.Button("Skip to Next Phase"))
                SkipToNextPhase();
            if (GUILayout.Button("End Upgrade Phase"))
                EndUpgradePhase();

            Section("TIMER ADJUST");
            GUILayout.BeginHorizontal();
            if (Btn("+5s")) _clock.ResetTimer(_clock.RemainingValue + 5f);
            if (Btn("+10s")) _clock.ResetTimer(_clock.RemainingValue + 10f);
            if (Btn("-5s")) _clock.ResetTimer(Mathf.Max(0.1f, _clock.RemainingValue - 5f));
            if (Btn("-10s")) _clock.ResetTimer(Mathf.Max(0.1f, _clock.RemainingValue - 10f));
            GUILayout.EndHorizontal();

            Section("SHORTCUTS");
            Label("F1: Toggle overlay");
            Label("F2: Pause/Resume");
            Label("F3: Skip to next phase");
            Label("F4: Force slime spawn");
            Label("F5: Restart scene");
            Label("F9: Late Game preset");
            Label("F10: Max Build preset");
        }

        // ================================================================
        // Actions
        // ================================================================

        private void TogglePause()
        {
            if (_clock.IsPausedValue) _clock.Resume();
            else _clock.Pause();
        }

        private void SkipToNextPhase()
        {
            _clock.ResetTimer(0.01f);
            _clock.Resume();
        }

        private void EndUpgradePhase()
        {
            _players.Draft1.Skip();
            _players.Draft2.Skip();
        }

        private void KillAllSlimes()
        {
            var all = new List<SlimeModel>(_slimeRegistry.GetAll());
            foreach (var s in all)
            {
                s.Kill();
                _slimeRegistry.Remove(s.Id);
            }
        }

        private void ShrinkTo(int targetSize)
        {
            while (_stage.Bounds.Current.Width > targetSize)
                _shrinkService.ShrinkOuterRing(_stage);
        }

        private void ApplyAllCategory(PlayerModel player, string category)
        {
            foreach (var id in _upgradeValues)
            {
                if (id.ToString().StartsWith(category, StringComparison.Ordinal))
                    _upgradeApply.Apply(id, player);
            }
        }

        private void ApplyCommonUpgradesNTimes(PlayerModel player, int n)
        {
            var commons = new[]
            {
                UpgradeId.FireFlightRange, UpgradeId.FireEffectRange, UpgradeId.FireDamage,
                UpgradeId.FireDuration, UpgradeId.FireCooldown,
                UpgradeId.BreakFlightRange, UpgradeId.BreakEffectRange, UpgradeId.BreakDamage,
                UpgradeId.BreakCollapseTime, UpgradeId.BreakCooldown,
                UpgradeId.MoveSpeed,
            };
            for (int i = 0; i < n; i++)
                foreach (var id in commons)
                    _upgradeApply.Apply(id, player);
        }

        // --- Presets ---

        private void PresetLateGame()
        {
            ShrinkTo(20);
            _players.Player1.Stats.AddCoins(30);
            _players.Player2.Stats.AddCoins(30);
            _slimeSpawnService.SpawnIfNeeded();
        }

        private void PresetMaxBuild()
        {
            ApplyCommonUpgradesNTimes(_players.Player1, 3);
            ApplyCommonUpgradesNTimes(_players.Player2, 3);
        }

        private void PresetLowHp()
        {
            SetHpTo(_players.Player1, 2);
            SetHpTo(_players.Player2, 2);
        }

        private void PresetEconomy()
        {
            _players.Player1.Stats.AddCoins(100);
            _players.Player2.Stats.AddCoins(100);
            SkipToNextPhase();
        }

        private static void SetHpTo(PlayerModel player, int hp)
        {
            int current = player.Stats.CurrentHp.CurrentValue;
            if (current > hp)
                player.Stats.TakeDamage(current - hp);
            else if (current < hp)
                player.Stats.Heal(hp - current);
        }

        private static void RestartScene()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        // ================================================================
        // GUI Helpers
        // ================================================================

        private static void Section(string title)
        {
            GUILayout.Space(6);
            GUILayout.Label(title, BoldLabel());
        }

        private static void Label(string text)
        {
            GUILayout.Label(text);
        }

        private static bool Btn(string text)
        {
            return GUILayout.Button(text, GUILayout.ExpandWidth(false));
        }

        private static void CompareRow(string label, string v1, string v2)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(80));
            GUILayout.Label(v1, GUILayout.Width(100));
            GUILayout.Label(v2, GUILayout.Width(100));
            GUILayout.EndHorizontal();
        }

        private static GUIStyle BoldLabel()
        {
            var style = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            return style;
        }
    }
}
