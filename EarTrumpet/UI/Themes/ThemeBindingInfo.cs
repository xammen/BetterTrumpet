using System;
using System.Reflection;
using System.Windows;

namespace EarTrumpet.UI.Themes
{
    class ThemeBindingInfo<T>
    {
        private readonly string _propertyName;
        private readonly string _value;
        private readonly Func<DependencyObject, string, T> _applyCallback;
        private WeakReference<DependencyObject> _element;
        private bool _isAttached;
        private T _initialValue;

        public ThemeBindingInfo(DependencyObject element, string value, string propertyName, Func<DependencyObject, string, T> applyCallback)
        {
            _propertyName = propertyName;
            _element = new WeakReference<DependencyObject>(element);
            _value = value;
            _applyCallback = applyCallback;

            ApplyValue(element);

            if (element is FrameworkElement)
            {
                ((FrameworkElement)element).Loaded += Element_Loaded;
            }
            else if (element is FrameworkContentElement)
            {
                ((FrameworkContentElement)element).Loaded += Element_Loaded;
            }

            // Options.Source changes are picked up through that property's own metadata callback,
            // which routes back here via Brush.ReapplyBindings. It used to be a per-instance
            // DependencyPropertyDescriptor.AddValueChanged subscription, which roots the element in
            // a static table until RemoveValueChanged runs.
        }

        public void Leaving()
        {
            if (_element.TryGetTarget(out var element))
            {
                UnregisterLoaded(element);

                if (_isAttached)
                {
                    WritePropertyValue(element, _initialValue);
                }
            }

            _isAttached = false;
            _element = null;
            _initialValue = default(T);
        }

        private void Element_Loaded(object sender, RoutedEventArgs e)
        {
            if (_element.TryGetTarget(out var element))
            {
                UnregisterLoaded(element);
                ApplyValue(element);
            }
        }

        private void UnregisterLoaded(DependencyObject element)
        {
            if (element is FrameworkContentElement)
            {
                ((FrameworkContentElement)element).Loaded -= Element_Loaded;
            }
            else if (element is FrameworkElement)
            {
                ((FrameworkElement)element).Loaded -= Element_Loaded;
            }
        }

        public void ApplyValue(DependencyObject element)
        {
            var type = Options.GetSource(element);
            if (type != null)
            {
                if (!_isAttached)
                {
                    _isAttached = true;
                    _initialValue = (T)ReadPropertyValue(element);
                }
                WritePropertyValue(element, _applyCallback.Invoke(element, _value));
            }
        }

        /// <summary>
        /// Repaints this binding. Driven by Brush, which owns the single subscription to
        /// Manager.ThemeChanged on behalf of every binding in its registry; subscribing from here
        /// would root this instance in that singleton for the life of the process.
        /// </summary>
        internal void ThemeChanged()
        {
            // Not attached means Options.Source has never resolved, so there is no captured initial
            // value to restore later and nothing to repaint. Options.Source changing will apply it.
            if (!_isAttached)
            {
                return;
            }

            if ((_element != null) && _element.TryGetTarget(out var element))
            {
                WritePropertyValue(element, _applyCallback.Invoke(element, _value));
            }
        }

        private PropertyInfo GetProperty(DependencyObject element) => element.GetType().GetProperty(_propertyName, BindingFlags.Public | BindingFlags.Instance);
        private object ReadPropertyValue(DependencyObject element) => GetProperty(element).GetValue(element);
        private void WritePropertyValue(DependencyObject element, object value) => GetProperty(element).SetValue(element, value);
    }
}
