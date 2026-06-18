using System;
using System.Diagnostics;
using System.Net.Http;
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

        public ChangelogWindow()
        {
            InitializeComponent();
            VersionBadge.Text = $"v{App.PackageVersion}";
            Loaded += OnLoaded;
            StartPulseAnimation();
        }

        private void StartPulseAnimation()
        {
            var radiusAnim = new DoubleAnimation
            {
                From = 0.6,
                To = 1.0,
                Duration = TimeSpan.FromSeconds(3),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };

            PulseGradient.BeginAnimation(RadialGradientBrush.RadiusXProperty, radiusAnim);
            PulseGradient.BeginAnimation(RadialGradientBrush.RadiusYProperty, radiusAnim);
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            ContentPanel.Children.Add(new TextBlock
            {
                Text = EarTrumpet.Properties.Resources.ChangelogLoading,
                FontSize = 14,
                Foreground = new SolidColorBrush(Colors.Gray),
                Margin = new Thickness(0, 20, 0, 0)
            });

            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "BetterTrumpet");
                    client.Timeout = TimeSpan.FromSeconds(10);

                    var response = await client.GetStringAsync(GitHubApiUrl);
                    var json = JObject.Parse(response);
                    var body = json["body"]?.ToString() ?? "";

                    ContentPanel.Children.Clear();

                    if (!string.IsNullOrWhiteSpace(body))
                    {
                        ParseMarkdown(body);
                    }
                    else
                    {
                        ContentPanel.Children.Add(new TextBlock
                        {
                            Text = EarTrumpet.Properties.Resources.ChangelogNoNotes,
                            FontSize = 14,
                            Foreground = new SolidColorBrush(Colors.Gray)
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Changelog failed: {ex.Message}");
                ContentPanel.Children.Clear();
                ContentPanel.Children.Add(new TextBlock
                {
                    Text = EarTrumpet.Properties.Resources.ChangelogLoadError,
                    FontSize = 14,
                    Foreground = new SolidColorBrush(Colors.Gray)
                });
            }
        }

        private void Continue_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) Close();
        }

        private void ParseMarkdown(string markdown)
        {
            var lines = markdown.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    // Empty line spacing
                    ContentPanel.Children.Add(new TextBlock { Height = 8 });
                    continue;
                }

                // Headers
                if (line.StartsWith("### "))
                {
                    ContentPanel.Children.Add(new TextBlock
                    {
                        Text = line.Substring(4),
                        FontSize = 16,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Colors.White),
                        Margin = new Thickness(0, 16, 0, 8)
                    });
                }
                else if (line.StartsWith("## "))
                {
                    ContentPanel.Children.Add(new TextBlock
                    {
                        Text = line.Substring(3),
                        FontSize = 18,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Colors.White),
                        Margin = new Thickness(0, 20, 0, 12)
                    });
                }
                else if (line.StartsWith("# "))
                {
                    ContentPanel.Children.Add(new TextBlock
                    {
                        Text = line.Substring(2),
                        FontSize = 20,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Colors.White),
                        Margin = new Thickness(0, 24, 0, 16)
                    });
                }
                // List items
                else if (line.StartsWith("- ") || line.StartsWith("* "))
                {
                    var text = line.Substring(2).Trim();

                    // Handle bold **text**
                    var textBlock = new TextBlock
                    {
                        FontSize = 14,
                        Foreground = new SolidColorBrush(Color.FromRgb(176, 176, 176)),
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, 8)
                    };

                    ParseInlineFormatting(textBlock, "• " + text);
                    ContentPanel.Children.Add(textBlock);
                }
                // Regular paragraph
                else
                {
                    var textBlock = new TextBlock
                    {
                        FontSize = 14,
                        Foreground = new SolidColorBrush(Color.FromRgb(176, 176, 176)),
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, 8)
                    };

                    ParseInlineFormatting(textBlock, line);
                    ContentPanel.Children.Add(textBlock);
                }
            }
        }

        private void ParseInlineFormatting(TextBlock textBlock, string text)
        {
            var parts = System.Text.RegularExpressions.Regex.Split(text, @"(\*\*.*?\*\*|`.*?`)");

            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;

                if (part.StartsWith("**") && part.EndsWith("**"))
                {
                    // Bold
                    textBlock.Inlines.Add(new System.Windows.Documents.Run(part.Substring(2, part.Length - 4))
                    {
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Colors.White)
                    });
                }
                else if (part.StartsWith("`") && part.EndsWith("`"))
                {
                    // Code
                    textBlock.Inlines.Add(new System.Windows.Documents.Run(part.Substring(1, part.Length - 2))
                    {
                        FontFamily = new FontFamily("Consolas"),
                        Background = new SolidColorBrush(Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF)),
                        Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 212))
                    });
                }
                else
                {
                    textBlock.Inlines.Add(new System.Windows.Documents.Run(part));
                }
            }
        }
    }
}
