using System.Windows;
using System.Windows.Input;

namespace JGF.Desktop.Common;

public static class KeyboardNavigationHelper
{
    public static readonly DependencyProperty EnableEnterKeyNavigationProperty =
        DependencyProperty.RegisterAttached(
            "EnableEnterKeyNavigation",
            typeof(bool),
            typeof(KeyboardNavigationHelper),
            new UIPropertyMetadata(false, OnEnableEnterKeyNavigationChanged));

    public static bool GetEnableEnterKeyNavigation(DependencyObject obj) =>
        (bool)obj.GetValue(EnableEnterKeyNavigationProperty);

    public static void SetEnableEnterKeyNavigation(DependencyObject obj, bool value) =>
        obj.SetValue(EnableEnterKeyNavigationProperty, value);

    private static void OnEnableEnterKeyNavigationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UIElement element)
        {
            if ((bool)e.NewValue)
            {
                element.PreviewKeyDown += OnPreviewKeyDown;
            }
            else
            {
                element.PreviewKeyDown -= OnPreviewKeyDown;
            }
        }
    }

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            // If the element is a multi-line TextBox and AcceptsReturn is true, let Enter create a new line
            if (Keyboard.FocusedElement is System.Windows.Controls.TextBox tb && tb.AcceptsReturn)
            {
                return;
            }

            // If the focused element is a Button, let Enter click the button
            if (Keyboard.FocusedElement is System.Windows.Controls.Button)
            {
                return;
            }

            e.Handled = true;
            var request = new TraversalRequest(FocusNavigationDirection.Next);
            if (Keyboard.FocusedElement is UIElement focusedElement)
            {
                focusedElement.MoveFocus(request);
            }
        }
    }
}

