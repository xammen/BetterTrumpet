using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;

namespace EarTrumpet.UI.Themes
{
    public static class Brush
    {
        // Weak keys. Elements that are rebuilt repeatedly would otherwise be pinned here for the
        // life of the process: the tray context menu creates a fresh ContextMenu and a fresh set of
        // MenuItem containers on every right-click, each carrying several theme bindings.
        //
        // Only ever touched on the UI thread: every writer arrives through a DependencyProperty
        // changed callback, and the reader below runs on Manager's ThemeChanged. Neither the outer
        // Dictionary nor ConditionalWeakTable.Add is safe against a concurrent writer, and Add
        // throws if the key is already present, hence the Remove-then-Add below.
        private static readonly Dictionary<string, ConditionalWeakTable<DependencyObject, ThemeBindingInfo<System.Windows.Media.Brush>>> _bindingInfo =
            new Dictionary<string, ConditionalWeakTable<DependencyObject, ThemeBindingInfo<System.Windows.Media.Brush>>>();

        private static bool _isSubscribedToThemeChanged;

        private static void ImplementPropertyChanged(string propertyName, DependencyObject dependencyObject, object newValue)
        {
            var value = (string)newValue;

            EnsureSubscribedToThemeChanged();

            if (!_bindingInfo.TryGetValue(propertyName, out var bindings))
            {
                bindings = new ConditionalWeakTable<DependencyObject, ThemeBindingInfo<System.Windows.Media.Brush>>();
                _bindingInfo[propertyName] = bindings;
            }

            if (bindings.TryGetValue(dependencyObject, out var outgoing))
            {
                bindings.Remove(dependencyObject);
                outgoing.Leaving();
            }

            if (!string.IsNullOrWhiteSpace(value))
            {
                bindings.Add(dependencyObject, new ThemeBindingInfo<System.Windows.Media.Brush>(dependencyObject, value, propertyName, BrushValueParser.Parse));
            }
        }

        /// <summary>
        /// Re-applies the theme bindings held for an element. Called when Options.Source changes,
        /// since a binding created before Source inherited from the parent could not resolve a
        /// theme yet.
        /// </summary>
        internal static void ReapplyBindings(DependencyObject dependencyObject)
        {
            // Also here, not just in ImplementPropertyChanged: a binding created while Manager did
            // not yet exist skips the subscription, and this is the path that then attaches it.
            EnsureSubscribedToThemeChanged();

            foreach (var bindings in _bindingInfo.Values)
            {
                if (bindings.TryGetValue(dependencyObject, out var info))
                {
                    info.ApplyValue(dependencyObject);
                }
            }
        }

        // One subscription for the whole registry, rather than one per ThemeBindingInfo. Manager is
        // a singleton, so a per-instance subscription roots the instance in its delegate list, and
        // a binding cannot unsubscribe itself when its element is simply discarded -- nothing calls
        // Leaving() in that case. That defeats the weak keys above: the element would be collected
        // but its binding would not, so the delegate list would grow by one entry per theme binding
        // per rebuild for the life of the process (roughly a hundred per tray right-click), and
        // every theme change would walk all of them.
        private static void EnsureSubscribedToThemeChanged()
        {
            if (_isSubscribedToThemeChanged || Manager.Current == null)
            {
                return;
            }

            _isSubscribedToThemeChanged = true;
            Manager.Current.ThemeChanged += OnThemeChanged;
        }

        private static void OnThemeChanged()
        {
            // Materialized before dispatching: a handler that sets a theme binding would otherwise
            // mutate what is being enumerated -- the ConditionalWeakTable it lands in, and the outer
            // Dictionary too if it is the first binding for that property name. Collected keys are
            // already absent from the enumeration.
            var bindings = _bindingInfo.Values.SelectMany(table => table).Select(entry => entry.Value).ToList();
            foreach (var binding in bindings)
            {
                binding.ThemeChanged();
            }
        }

        public static string GetForeground(DependencyObject obj) => (string)obj.GetValue(ForegroundProperty);
        public static void SetForeground(DependencyObject obj, string value) => obj.SetValue(ForegroundProperty, value);
        public static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.RegisterAttached("Foreground", typeof(string), typeof(Brush), new PropertyMetadata("", ForegroundChanged));
        private static void ForegroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ImplementPropertyChanged("Foreground", d, e.NewValue);

        public static string GetBackground(DependencyObject obj) => (string)obj.GetValue(BackgroundProperty);
        public static void SetBackground(DependencyObject obj, string value) => obj.SetValue(BackgroundProperty, value);
        public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.RegisterAttached("Background", typeof(string), typeof(Brush), new PropertyMetadata("", BackgroundChanged));
        private static void BackgroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ImplementPropertyChanged("Background", d, e.NewValue);

        public static string GetBorderBrush(DependencyObject obj) => (string)obj.GetValue(BorderBrushProperty);
        public static void SetBorderBrush(DependencyObject obj, string value) => obj.SetValue(BorderBrushProperty, value);
        public static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.RegisterAttached("BorderBrush", typeof(string), typeof(Brush), new PropertyMetadata("", BorderBrushChanged));
        private static void BorderBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ImplementPropertyChanged("BorderBrush", d, e.NewValue);

        public static string GetStroke(DependencyObject obj) => (string)obj.GetValue(StrokeProperty);
        public static void SetStroke(DependencyObject obj, string value) => obj.SetValue(StrokeProperty, value);
        public static readonly DependencyProperty StrokeProperty =
        DependencyProperty.RegisterAttached("Stroke", typeof(string), typeof(Brush), new PropertyMetadata("", StrokeChanged));
        private static void StrokeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ImplementPropertyChanged("Stroke", d, e.NewValue);

        public static string GetFill(DependencyObject obj) => (string)obj.GetValue(FillProperty);
        public static void SetFill(DependencyObject obj, string value) => obj.SetValue(FillProperty, value);
        public static readonly DependencyProperty FillProperty =
        DependencyProperty.RegisterAttached("Fill", typeof(string), typeof(Brush), new PropertyMetadata("", FillChanged));
        private static void FillChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ImplementPropertyChanged("Fill", d, e.NewValue);

        public static string GetSelectionBrush(DependencyObject obj) => (string)obj.GetValue(SelectionBrushProperty);
        public static void SetSelectionBrush(DependencyObject obj, string value) => obj.SetValue(SelectionBrushProperty, value);
        public static readonly DependencyProperty SelectionBrushProperty =
        DependencyProperty.RegisterAttached("SelectionBrush", typeof(string), typeof(Brush), new PropertyMetadata("", SelectionBrushChanged));
        private static void SelectionBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ImplementPropertyChanged("SelectionBrush", d, e.NewValue);

        public static string GetCaretBrush(DependencyObject obj) => (string)obj.GetValue(CaretBrushProperty);
        public static void SetCaretBrush(DependencyObject obj, string value) => obj.SetValue(CaretBrushProperty, value);
        public static readonly DependencyProperty CaretBrushProperty =
        DependencyProperty.RegisterAttached("CaretBrush", typeof(string), typeof(Brush), new PropertyMetadata("", CaretBrushChanged));
        private static void CaretBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ImplementPropertyChanged("CaretBrush", d, e.NewValue);
    }
}
