using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace EarTrumpet.Extensions
{
    public static class DependencyObjectExtensions
    {
        public static T FindVisualParent<T>(this DependencyObject obj) where T : DependencyObject
        {
            var parentObject = GetParentObject(obj);
            if (parentObject == null)
            {
                return null;
            }

            return (parentObject is T) ? (T)parentObject : FindVisualParent<T>(parentObject);
        }

        public static DependencyObject GetParentObject(this DependencyObject child)
        {
            if (child == null)
            {
                return null;
            }

            var contentElement = child as ContentElement;
            if (contentElement != null)
            {
                var parent = ContentOperations.GetParent(contentElement);
                if (parent != null)
                {
                    return parent;
                }

                var frameworkContentElement = contentElement as FrameworkContentElement;
                return frameworkContentElement != null ? frameworkContentElement.Parent : null;
            }

            var frameworkElement = child as FrameworkElement;
            if (frameworkElement != null)
            {
                var parent = frameworkElement.Parent;
                if (parent != null)
                {
                    return parent;
                }
            }
            return VisualTreeHelper.GetParent(child);
        }

        public static childItem FindVisualChild<childItem>(this DependencyObject obj) where childItem : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is childItem)
                {
                    return (childItem)child;
                }
                else
                {
                    var childOfChild = FindVisualChild<childItem>(child);
                    if (childOfChild != null)
                    {
                        return childOfChild;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Enumerates every descendant of <paramref name="obj"/> of type T in visual
        /// (top-to-bottom) order. Does not recurse into a match's own subtree, so e.g.
        /// AppItemView rows are returned as flat siblings, not nested.
        /// </summary>
        public static IEnumerable<T> FindVisualChildren<T>(this DependencyObject obj) where T : DependencyObject
        {
            if (obj == null)
            {
                yield break;
            }

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                if (child is T match)
                {
                    yield return match;
                }
                else
                {
                    foreach (var descendant in FindVisualChildren<T>(child))
                    {
                        yield return descendant;
                    }
                }
            }
        }
    }
}
