using Automatization.Models;
using Automatization.Settings;
using Automatization.Types;
using System.Windows;
using System.Windows.Controls;

namespace Automatization.UI
{
    public partial class GameDataWindow : Window
    {
        private class ProtectionEntry
        {
            public DamageType DamageType { get; init; }
            public string DamageTypeName => DamageType.ToString();
            public double Percentage { get; set; }
        }

        private class GamePlanEntry
        {
            public int PlayerNumber { get; init; }
            public string Role { get; init; } = string.Empty;
            public string TurretName { get; init; } = string.Empty;
            public string Range { get; init; } = string.Empty;
            public string PaintName { get; init; } = string.Empty;
        }

        private readonly GameData _gameData;
        private bool _suppressEvents = false;

        public GameDataWindow()
        {
            InitializeComponent();
            _gameData = GameData.Load();

            RefreshPaintsList();
            TurretsDataGrid.ItemsSource = _gameData.Turrets;

            for (int i = 2; i <= 10; i++)
                PlayerCountComboBox.Items.Add(i);
            PlayerCountComboBox.SelectedIndex = 0;
        }

        #region Paints

        private void RefreshPaintsList()
        {
            Paint? selected = PaintsListBox.SelectedItem as Paint;
            PaintsListBox.ItemsSource = null;
            PaintsListBox.ItemsSource = _gameData.Paints;
            if (selected != null && _gameData.Paints.Contains(selected))
                PaintsListBox.SelectedItem = selected;
        }

        private void PaintsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PaintsListBox.SelectedItem is Paint paint)
            {
                _suppressEvents = true;
                PaintNameTextBox.Text = paint.Name;
                ProtectionsDataGrid.ItemsSource = BuildProtectionEntries(paint);
                _suppressEvents = false;
            }
            else
            {
                PaintNameTextBox.Text = string.Empty;
                ProtectionsDataGrid.ItemsSource = null;
            }
        }

        private static List<ProtectionEntry> BuildProtectionEntries(Paint paint)
        {
            return Enum.GetValues<DamageType>()
                .Select(dt => new ProtectionEntry
                {
                    DamageType = dt,
                    Percentage = paint.Protections.GetValueOrDefault(dt.ToString(), 0)
                })
                .ToList();
        }

        private void PaintNameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressEvents || PaintsListBox.SelectedItem is not Paint paint) return;

            paint.Name = PaintNameTextBox.Text.Trim();
            _gameData.Save();
            RefreshPaintsList();
        }

        private void ProtectionsDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit) return;
            if (PaintsListBox.SelectedItem is not Paint paint) return;

            _ = Dispatcher.BeginInvoke(() =>
            {
                if (ProtectionsDataGrid.ItemsSource is not List<ProtectionEntry> entries) return;

                foreach (ProtectionEntry entry in entries)
                    paint.Protections[entry.DamageType.ToString()] = entry.Percentage;

                _gameData.Save();
            });
        }

        private void AddPaintButton_Click(object sender, RoutedEventArgs e)
        {
            Paint newPaint = new() { Name = "New Paint" };
            _gameData.Paints.Add(newPaint);
            _gameData.Save();
            RefreshPaintsList();
            PaintsListBox.SelectedItem = newPaint;
            PaintNameTextBox.Focus();
            PaintNameTextBox.SelectAll();
        }

        private void RemovePaintButton_Click(object sender, RoutedEventArgs e)
        {
            if (PaintsListBox.SelectedItem is not Paint paint) return;
            _gameData.Paints.Remove(paint);
            _gameData.Save();
            RefreshPaintsList();
        }

        #endregion

        #region Game Plan

        private void GeneratePlanButton_Click(object sender, RoutedEventArgs e)
        {
            MapSize mapSize = MapLargeRadio.IsChecked == true ? MapSize.Large
                            : MapMediumRadio.IsChecked == true ? MapSize.Medium
                            : MapSize.Small;

            int playerCount = PlayerCountComboBox.SelectedItem is int n ? n : 2;
            bool wantDefense = WantDefenseCheckBox.IsChecked == true;

            List<GamePlanEntry> plan = GenerateGamePlan(mapSize, playerCount, wantDefense);

            GamePlanDataGrid.ItemsSource = plan;
            GamePlanDataGrid.Visibility = Visibility.Visible;
            GamePlanPlaceholder.Visibility = Visibility.Collapsed;
        }

        private List<GamePlanEntry> GenerateGamePlan(MapSize mapSize, int playerCount, bool wantDefense)
        {
            var shortPool  = _gameData.Turrets.Where(t => t.Range == TurretRange.Short).ToList();
            var mediumPool = _gameData.Turrets.Where(t => t.Range == TurretRange.Medium).ToList();
            var longPool   = _gameData.Turrets.Where(t => t.Range == TurretRange.Long).ToList();

            // Get desired distribution for this map + player count
            var (shortCount, mediumCount, longCount) = GetTurretDistribution(mapSize, playerCount);

            // Cap at available pool sizes, redistribute overflow to long
            int overflow = Math.Max(0, shortCount - shortPool.Count) +
                           Math.Max(0, mediumCount - mediumPool.Count);
            shortCount  = Math.Min(shortCount, shortPool.Count);
            mediumCount = Math.Min(mediumCount, mediumPool.Count);
            longCount   = Math.Min(longPool.Count, longCount + overflow);

            // Select turrets per range
            List<Turret> selectedLong   = SelectFromPool(longPool, longCount, wantDefense);
            List<Turret> selectedMedium = SelectFromPool(mediumPool, mediumCount, wantDefense);
            List<Turret> selectedShort  = SelectShortTurrets(shortPool, shortCount);

            // Long → medium → short order in plan
            var allSelected = selectedLong.Concat(selectedMedium).Concat(selectedShort).ToList();

            // Assign defend roles to CanDefend turrets (up to ~¼ of team)
            int defenderCount = wantDefense ? Math.Max(1, playerCount / 4) : 0;
            int defendAssigned = 0;

            // Assign paints — each player gets a unique paint
            var usedPaints = new HashSet<string>();
            var plan = new List<GamePlanEntry>();

            for (int i = 0; i < allSelected.Count; i++)
            {
                Turret turret = allSelected[i];

                bool isDefender = wantDefense && turret.CanDefend && defendAssigned < defenderCount;
                if (isDefender) defendAssigned++;

                Paint? paint = FindBestUnusedPaint(turret.Range, mapSize, usedPaints);
                if (paint != null) usedPaints.Add(paint.Name);

                plan.Add(new GamePlanEntry
                {
                    PlayerNumber = i + 1,
                    Role         = isDefender ? "Defend" : "Attack",
                    TurretName   = turret.Name,
                    Range        = turret.Range.ToString(),
                    PaintName    = paint?.Name ?? "None"
                });
            }

            return plan;
        }

        // Returns (shortCount, mediumCount, longCount) for the given map and player count.
        // Rules provided by the user; large map extrapolated from medium with more long-range bias.
        private static (int Short, int Medium, int Long) GetTurretDistribution(MapSize mapSize, int playerCount)
        {
            return (mapSize, playerCount) switch
            {
                // ── Small map ──────────────────────────────────
                (MapSize.Small,  2) => (1, 1, 0),
                (MapSize.Small,  3) => (1, 1, 1),
                (MapSize.Small,  4) => (1, 2, 1),
                (MapSize.Small,  5) => (1, 2, 2),
                (MapSize.Small,  6) => (1, 3, 2),
                (MapSize.Small,  7) => (2, 3, 2),
                (MapSize.Small,  8) => (2, 3, 3),
                (MapSize.Small,  9) => (2, 4, 3),
                (MapSize.Small, 10) => (2, 4, 4),

                // ── Medium map ─────────────────────────────────
                (MapSize.Medium,  2) => (0, 0, 2),
                (MapSize.Medium,  3) => (0, 1, 2),
                (MapSize.Medium,  4) => (1, 1, 2),
                (MapSize.Medium,  5) => (1, 2, 2),
                (MapSize.Medium,  6) => (2, 2, 2),
                (MapSize.Medium,  7) => (2, 2, 3),
                (MapSize.Medium,  8) => (2, 3, 3),
                (MapSize.Medium,  9) => (2, 3, 4),
                (MapSize.Medium, 10) => (3, 3, 4),

                // ── Large map (more long-range biased) ─────────
                (MapSize.Large,  2) => (0, 0, 2),
                (MapSize.Large,  3) => (0, 1, 2),
                (MapSize.Large,  4) => (0, 1, 3),
                (MapSize.Large,  5) => (1, 1, 3),
                (MapSize.Large,  6) => (1, 2, 3),
                (MapSize.Large,  7) => (1, 2, 4),
                (MapSize.Large,  8) => (1, 3, 4),
                (MapSize.Large,  9) => (2, 3, 4),
                (MapSize.Large, 10) => (2, 3, 5),

                _ => (1, 1, Math.Max(0, playerCount - 2))
            };
        }

        // Select N turrets from a pool. If preferring defenders, CanDefend turrets come first.
        private static List<Turret> SelectFromPool(List<Turret> pool, int count, bool preferDefenders)
        {
            if (count <= 0) return [];
            return pool
                .OrderByDescending(t => preferDefenders && t.CanDefend ? 1 : 0)
                .Take(count)
                .ToList();
        }

        // Select short-range turrets. Never combine Firebird + Freeze (they counter each other).
        private static List<Turret> SelectShortTurrets(List<Turret> pool, int count)
        {
            if (count <= 0) return [];
            if (count >= 3)  return pool.Take(3).ToList();
            if (count == 1)  return pool.Take(1).ToList();

            // count == 2: ensure Firebird and Freeze don't both appear
            var top2 = pool.Take(2).ToList();
            bool conflict = top2.Any(t => t.Name == "Firebird") && top2.Any(t => t.Name == "Freeze");
            if (!conflict) return top2;

            // Replace Freeze with Isida if available; otherwise just take Firebird alone
            var isida = pool.FirstOrDefault(t => t.Name == "Isida");
            if (isida != null)
                return [top2.First(t => t.Name == "Firebird"), isida];

            return [top2.First(t => t.Name == "Firebird")];
        }

        // Pick the highest-scoring unused paint.
        // Score = protection against threats weighted by turret range (×2) + map dominant threats (×1).
        private Paint? FindBestUnusedPaint(TurretRange turretRange, MapSize mapSize, HashSet<string> usedPaints)
        {
            return _gameData.Paints
                .Where(p => !usedPaints.Contains(p.Name))
                .OrderByDescending(p => ScorePaint(p, turretRange, mapSize))
                .FirstOrDefault();
        }

        private static double ScorePaint(Paint paint, TurretRange turretRange, MapSize mapSize)
        {
            // Weapons this player will mostly fight (based on who they engage at close quarters)
            HashSet<string> rangeThreat = turretRange switch
            {
                TurretRange.Short => ["Firebird", "Isida", "Freeze", "Ricochet", "Hammer", "Twins"],
                TurretRange.Long  => ["Railgun", "Smoky", "Vulcan", "Thunder", "Shaft"],
                _                 => ["Railgun", "Smoky", "Firebird", "Vulcan", "Twins",
                                      "Isida", "Thunder", "Hammer", "Freeze", "Ricochet", "Shaft"]
            };

            // Dominant weapons on this map size (everyone faces these regardless of their turret)
            HashSet<string> mapThreat = mapSize switch
            {
                MapSize.Large => ["Railgun", "Smoky", "Vulcan", "Thunder", "Shaft"],
                MapSize.Small => ["Firebird", "Isida", "Freeze", "Ricochet", "Hammer", "Twins"],
                _             => ["Railgun", "Smoky", "Firebird", "Vulcan", "Twins",
                                  "Isida", "Thunder", "Hammer", "Freeze", "Ricochet", "Shaft"]
            };

            double score = 0;
            foreach (var (weapon, protection) in paint.Protections)
            {
                int weight = 0;
                if (rangeThreat.Contains(weapon)) weight += 2;
                if (mapThreat.Contains(weapon))   weight += 1;
                score += protection * weight;
            }

            return score;
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            _gameData.Save();
            base.OnClosed(e);
        }
    }
}
