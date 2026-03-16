using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace EarTrumpet.UI.Views
{
    public partial class CommandPaletteWindow : Window
    {
        public class CommandEntry
        {
            public string Name { get; set; }
            public string Shortcut { get; set; }
            public string[] SearchTokens { get; set; }
            public Action Execute { get; set; }
        }

        private readonly List<CommandEntry> _allCommands;
        private bool _closing;

        public CommandPaletteWindow(List<CommandEntry> commands)
        {
            InitializeComponent();
            _allCommands = commands;
            ResultsList.ItemsSource = _allCommands;

            Loaded += (_, __) =>
            {
                SearchBox.Focus();
            };

            Closing += (_, __) => _closing = true;

            Deactivated += (_, __) =>
            {
                if (!_closing) Close();
            };
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var query = SearchBox.Text.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(query))
            {
                ResultsList.ItemsSource = _allCommands;
            }
            else
            {
                var filtered = _allCommands.Where(c => FuzzyMatch(c, query)).ToList();
                ResultsList.ItemsSource = filtered;
            }

            if (ResultsList.Items.Count > 0)
            {
                ResultsList.SelectedIndex = 0;
            }
        }

        private bool FuzzyMatch(CommandEntry entry, string query)
        {
            // Match against name and search tokens
            var name = entry.Name.ToLowerInvariant();
            if (name.Contains(query)) return true;

            if (entry.SearchTokens != null)
            {
                foreach (var token in entry.SearchTokens)
                {
                    if (token.ToLowerInvariant().Contains(query)) return true;
                }
            }

            // Fuzzy: check if all query chars appear in order
            int qi = 0;
            foreach (var ch in name)
            {
                if (qi < query.Length && ch == query[qi])
                {
                    qi++;
                }
            }
            return qi == query.Length;
        }

        private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Down:
                    if (ResultsList.Items.Count > 0)
                    {
                        ResultsList.SelectedIndex = Math.Min(ResultsList.SelectedIndex + 1, ResultsList.Items.Count - 1);
                        ResultsList.ScrollIntoView(ResultsList.SelectedItem);
                    }
                    e.Handled = true;
                    break;
                case Key.Up:
                    if (ResultsList.Items.Count > 0)
                    {
                        ResultsList.SelectedIndex = Math.Max(ResultsList.SelectedIndex - 1, 0);
                        ResultsList.ScrollIntoView(ResultsList.SelectedItem);
                    }
                    e.Handled = true;
                    break;
                case Key.Enter:
                    ExecuteSelected();
                    e.Handled = true;
                    break;
                case Key.Escape:
                    Close();
                    e.Handled = true;
                    break;
            }
        }

        private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ExecuteSelected();
        }

        private void ExecuteSelected()
        {
            var selected = ResultsList.SelectedItem as CommandEntry;
            if (selected != null)
            {
                _closing = true;
                Close();
                selected.Execute?.Invoke();
            }
        }
    }
}
