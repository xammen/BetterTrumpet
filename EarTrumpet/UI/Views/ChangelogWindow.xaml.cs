using EarTrumpet.Properties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Newtonsoft.Json.Linq;

namespace EarTrumpet.UI.Views
{
    public partial class ChangelogWindow : Window
    {
        private const string GitHubApiUrl = "https://api.github.com/repos/xammen/BetterTrumpet/releases/latest";

        private static readonly Brush _textPrimary;
        private static readonly Brush _textSecondary;
        private static readonly Brush _textMuted;
        private static readonly Brush _surfaceBrush;
        private static readonly Brush _cardBorder;
        private static readonly Brush _divider;
        private static readonly Brush _accentBrush;

        static ChangelogWindow()
        {
            _textPrimary = new SolidColorBrush(Color.FromArgb(0xF0, 0xFF, 0xFF, 0xFF));
            _textSecondary = new SolidColorBrush(Color.FromArgb(0xB0, 0xFF, 0xFF, 0xFF));
            _textMuted = new SolidColorBrush(Color.FromArgb(0x70, 0xFF, 0xFF, 0xFF));
            _surfaceBrush = new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x1E));
            _cardBorder = new SolidColorBrush(Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF));
            _divider = new SolidColorBrush(Color.FromArgb(0x10, 0xFF, 0xFF, 0xFF));
            _accentBrush = new SolidColorBrush(Color.FromRgb(0x3B, 0x9E, 0xFF));
        }

        public ChangelogWindow()
        {
            InitializeComponent();
            VersionBadge.Text = $"v{App.PackageVersion}";
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            AddLoadingIndicator();

            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "BetterTrumpet-Changelog");
                    client.Timeout = TimeSpan.FromSeconds(10);

                    var response = await client.GetStringAsync(GitHubApiUrl);
                    var json = JObject.Parse(response);

                    var body = json["body"]?.ToString() ?? "";

                    ContentPanel.Children.Clear();

                    if (!string.IsNullOrWhiteSpace(body))
                    {
                        ParseMarkdownToUI(body);
                    }
                    else
                    {
                        AddFallbackContent(Properties.Resources.ChangelogNoNotes);
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"ChangelogWindow: Failed to fetch release notes - {ex.Message}");
                ContentPanel.Children.Clear();
                AddFallbackContent(Properties.Resources.ChangelogLoadError);
            }

            AnimateContentIn();
        }

        private void ParseMarkdownToUI(string markdown)
        {
            var lines = markdown.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            StackPanel currentSection = null;
            var introLines = new List<string>();

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (line.StartsWith("## "))
                    continue;

                if (IsMarkdownImageOrLinkOnly(line))
                    continue;

                if (line.StartsWith("### "))
                {
                    FlushIntro(introLines);
                    introLines.Clear();

                    var title = line.Substring(4).Trim();
                    currentSection = new StackPanel();
                    var card = CreateSectionCard(title, currentSection);
                    ContentPanel.Children.Add(card);
                    continue;
                }

                if (line.StartsWith("- "))
                {
                    FlushIntro(introLines);
                    introLines.Clear();

                    var text = line.Substring(2).Trim();

                    if (currentSection == null)
                    {
                        currentSection = new StackPanel();
                        var card = CreateSectionCard("", currentSection);
                        ContentPanel.Children.Add(card);
                    }

                    AddBulletRow(currentSection, text);
                    continue;
                }

                if (currentSection == null)
                {
                    introLines.Add(StripMarkdownLinks(line));
                }
                else
                {
                    AddParagraph(currentSection, StripMarkdownLinks(line));
                }
            }

            FlushIntro(introLines);
        }

        private void FlushIntro(List<string> lines)
        {
            if (lines.Count == 0) return;

            var text = string.Join(" ", lines).Trim();
            if (string.IsNullOrWhiteSpace(text)) return;

            var tb = new TextBlock
            {
                Text = text,
                FontSize = 14,
                Foreground = _textSecondary,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 22,
                Margin = new Thickness(0, 0, 0, 24),
            };

            ContentPanel.Children.Add(tb);
        }

        private Border CreateSectionCard(string title, StackPanel content)
        {
            var root = new StackPanel();

            if (!string.IsNullOrWhiteSpace(title))
            {
                var header = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(20, 16, 20, 0),
                };

                header.Children.Add(new TextBlock
                {
                    Text = GetSectionGlyph(title),
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 15,
                    Foreground = _accentBrush,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 10, 0),
                });

                header.Children.Add(new TextBlock
                {
                    Text = title,
                    FontSize = 16,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = _textPrimary,
                    VerticalAlignment = VerticalAlignment.Center,
                });

                root.Children.Add(header);

                root.Children.Add(new Border
                {
                    Height = 1,
                    Background = _divider,
                    Margin = new Thickness(20, 12, 20, 4),
                });
            }

            root.Children.Add(content);

            return new Border
            {
                CornerRadius = new CornerRadius(8),
                Background = _surfaceBrush,
                BorderBrush = _cardBorder,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(0, 0, 0, 8),
                Margin = new Thickness(0, 0, 0, 16),
                Child = root,
            };
        }

        private void AddParagraph(Panel parent, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            var tb = new TextBlock
            {
                FontSize = 13,
                Foreground = _textMuted,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 20,
                Margin = new Thickness(20, 8, 20, 8),
            };

            ParseInlineMarkdown(tb, text);
            parent.Children.Add(tb);
        }

        private void AddBulletRow(Panel parent, string markdownText)
        {
            var cleanText = StripMarkdownLinks(markdownText);
            if (string.IsNullOrWhiteSpace(cleanText))
                return;

            var row = new Grid { Margin = new Thickness(20, 8, 20, 8) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var dot = new Border
            {
                Width = 6,
                Height = 6,
                CornerRadius = new CornerRadius(3),
                Background = _accentBrush,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 7, 0, 0),
            };
            Grid.SetColumn(dot, 0);
            row.Children.Add(dot);

            var textBlock = new TextBlock
            {
                FontSize = 14,
                Foreground = _textSecondary,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 22,
            };
            ParseInlineMarkdown(textBlock, cleanText);
            Grid.SetColumn(textBlock, 1);
            row.Children.Add(textBlock);

            parent.Children.Add(row);
        }

        private void ParseInlineMarkdown(TextBlock textBlock, string text)
        {
            int pos = 0;
            while (pos < text.Length)
            {
                int boldStart = text.IndexOf("**", pos);
                if (boldStart < 0)
                {
                    textBlock.Inlines.Add(new System.Windows.Documents.Run(text.Substring(pos)) { Foreground = textBlock.Foreground });
                    break;
                }

                if (boldStart > pos)
                {
                    textBlock.Inlines.Add(new System.Windows.Documents.Run(text.Substring(pos, boldStart - pos)) { Foreground = textBlock.Foreground });
                }

                int boldEnd = text.IndexOf("**", boldStart + 2);
                if (boldEnd < 0)
                {
                    textBlock.Inlines.Add(new System.Windows.Documents.Run(text.Substring(boldStart)) { Foreground = textBlock.Foreground });
                    break;
                }

                var boldText = text.Substring(boldStart + 2, boldEnd - boldStart - 2);
                textBlock.Inlines.Add(new System.Windows.Documents.Run(boldText)
                {
                    FontWeight = FontWeights.SemiBold,
                    Foreground = _textPrimary,
                });

                pos = boldEnd + 2;
            }
        }

        private bool IsMarkdownImageOrLinkOnly(string text)
        {
            var trimmed = text.Trim();
            return Regex.IsMatch(trimmed, @"^!\[[^\]]*\]\([^\)]*\)$") ||
                   Regex.IsMatch(trimmed, @"^\[[^\]]+\]\([^\)]*\)$");
        }

        private string StripMarkdownLinks(string text)
        {
            var withoutImages = Regex.Replace(text, @"!\[[^\]]*\]\([^\)]*\)", "");
            return Regex.Replace(withoutImages, @"\[([^\]]+)\]\([^\)]*\)", "$1").Trim();
        }

        private string GetSectionGlyph(string sectionTitle)
        {
            var lower = sectionTitle.ToLowerInvariant();
            if (lower.Contains("fix")) return "\xE90F";
            if (lower.Contains("new") || lower.Contains("feat") || lower.Contains("nouveau")) return "\xE710";
            if (lower.Contains("break")) return "\xE7BA";
            if (lower.Contains("perf")) return "\xE9F5";
            if (lower.Contains("under") || lower.Contains("capot") || lower.Contains("tech")) return "\xE756";
            return "\xE81C";
        }

        private void AddLoadingIndicator()
        {
            ContentPanel.Children.Clear();
            ContentPanel.Children.Add(new TextBlock
            {
                Text = Properties.Resources.ChangelogLoading,
                FontSize = 14,
                Foreground = _textMuted,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0),
            });
        }

        private void AddFallbackContent(string message)
        {
            ContentPanel.Children.Add(new TextBlock
            {
                Text = message,
                FontSize = 14,
                Foreground = _textSecondary,
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0),
            });
        }

        private void AnimateContentIn()
        {
            int i = 0;
            foreach (UIElement child in ContentPanel.Children)
            {
                child.Opacity = 0;
                child.RenderTransform = new TranslateTransform(0, 12);

                var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300))
                {
                    BeginTime = TimeSpan.FromMilliseconds(40 * i),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                var slide = new DoubleAnimation(12, 0, TimeSpan.FromMilliseconds(300))
                {
                    BeginTime = TimeSpan.FromMilliseconds(40 * i),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                child.BeginAnimation(OpacityProperty, fade);
                ((TranslateTransform)child.RenderTransform).BeginAnimation(TranslateTransform.YProperty, slide);
                i++;
            }
        }

        private void TitleBar_Drag(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        private void Close_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            Close();
        }

        private void Continue_Click(object sender, MouseButtonEventArgs e)
        {
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) Close();
        }
    }
}
