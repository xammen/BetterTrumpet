using System;
using System.ComponentModel;
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

            // Listen for Options.Source property changes to handle the case where
            // the brush is created before Options.Source has propagated from the parent.
            // This fixes the backdrop not rendering correctly on startup (GitHub #13).
            var descriptor = DependencyPropertyDescriptor.FromProperty(Options.SourceProperty, element.GetType());
            if (descriptor != null)
            {
                descriptor.AddValueChanged(element, OnOptionsSourceChanged);
            }
        }

        public void Leaving()
        {
            if (_element.TryGetTarget(out var element))
            {
                UnregisterLoaded(element);
                UnregisterOptionsSourceChanged(element);

                if (_isAttached)
                {
                    WritePropertyValue(element, _initialValue);
                }
            }

            _isAttached = false;
            _element = null;
            _initialValue = default(T);
            Manager.Current.ThemeChanged -= ThemeChanged;
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

        private void UnregisterOptionsSourceChanged(DependencyObject element)
        {
            var descriptor = DependencyPropertyDescriptor.FromProperty(Options.SourceProperty, element.GetType());
            if (descriptor != null)
            {
                descriptor.RemoveValueChanged(element, OnOptionsSourceChanged);
            }
        }

        private void OnOptionsSourceChanged(object sender, EventArgs e)
        {
            if (_element.TryGetTarget(out var element))
            {
                // Only apply if we haven't attached yet, or if we need to reapply due to theme change
                // This prevents duplicate application when the property inherits during construction
                ApplyValue(element);
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
                    Manager.Current.ThemeChanged += ThemeChanged;
                }
                WritePropertyValue(element, _applyCallback.Invoke(element, _value));
            }
        }

        private void ThemeChanged()
        {
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
