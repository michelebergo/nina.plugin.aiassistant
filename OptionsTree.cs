using System.Windows;
using System.Windows.Controls;

namespace NINA.Plugin.AIAssistant
{
    /// <summary>
    /// How the options page finds the result line that belongs to a button: walk up through the
    /// StackPanels that contain the button and take the first direct child TextBlock with that
    /// name. The layout must keep each button and its result in the same StackPanel tree; the
    /// options page test checks that for every pair.
    /// </summary>
    public static class OptionsTree
    {
        public static TextBlock? FindResultTextBlock(string name, FrameworkElement startElement)
        {
            var parent = startElement.Parent as FrameworkElement;
            while (parent != null)
            {
                if (parent is StackPanel stackPanel)
                {
                    foreach (var child in stackPanel.Children)
                    {
                        if (child is TextBlock tb && tb.Name == name)
                        {
                            return tb;
                        }
                    }
                }
                parent = parent.Parent as FrameworkElement;
            }
            return null;
        }
    }
}
