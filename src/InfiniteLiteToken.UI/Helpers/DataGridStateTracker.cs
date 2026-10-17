using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace InfiniteLiteToken.UI.Helpers
{
    public static class DataGridStateTracker
    {
        private static readonly string SettingsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "InfiniteLiteToken", "ui_datagrid_state.json");
        private static Dictionary<string, double[]> _cache = new();
        private static bool _appExitHooked = false;
        private static List<DataGrid> _activeGrids = new();

        public static readonly DependencyProperty StateIdProperty =
            DependencyProperty.RegisterAttached("StateId", typeof(string), typeof(DataGridStateTracker), new PropertyMetadata(null, OnStateIdChanged));

        public static void SetStateId(DependencyObject element, string value) => element.SetValue(StateIdProperty, value);
        public static string GetStateId(DependencyObject element) => (string)element.GetValue(StateIdProperty);

        private static void OnStateIdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DataGrid grid && e.NewValue is string stateId && !string.IsNullOrEmpty(stateId))
            {
                grid.Loaded += (s, args) => { LoadState(grid, stateId); if (!_activeGrids.Contains(grid)) _activeGrids.Add(grid); };
                grid.Unloaded += (s, args) => { SaveState(grid, stateId); _activeGrids.Remove(grid); };

                if (!_appExitHooked && Application.Current != null)
                {
                    Application.Current.Exit += (s, args) =>
                    {
                        foreach(var g in _activeGrids.ToList())
                        {
                            var id = GetStateId(g);
                            if (!string.IsNullOrEmpty(id)) SaveState(g, id);
                        }
                    };
                    _appExitHooked = true;
                }
            }
        }

        private static void LoadState(DataGrid grid, string stateId)
        {
            try
            {
                if (!File.Exists(SettingsFile)) return;
                if (_cache.Count == 0) _cache = JsonSerializer.Deserialize<Dictionary<string, double[]>>(File.ReadAllText(SettingsFile)) ?? new();

                if (_cache.TryGetValue(stateId, out var widths))
                {
                    for (int i = 0; i < grid.Columns.Count && i < widths.Length; i++)
                    {
                        if (widths[i] > 0)
                            grid.Columns[i].Width = new DataGridLength(widths[i], DataGridLengthUnitType.Star);
                    }
                }
            }
            catch { }
        }

        private static void SaveState(DataGrid grid, string stateId)
        {
            try
            {
                var totalWidth = grid.Columns.Sum(c => c.ActualWidth);
                if (totalWidth <= 0) return;
                var widths = grid.Columns.Select(c => c.ActualWidth / totalWidth).ToArray();
                _cache[stateId] = widths;

                var dir = Path.GetDirectoryName(SettingsFile);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir!);

                File.WriteAllText(SettingsFile, JsonSerializer.Serialize(_cache));
            }
            catch { }
        }
    }
}