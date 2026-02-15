using System;
using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Controls;

namespace NINA.Plugin.AIAssistant
{
    [Export(typeof(ResourceDictionary))]
    public partial class AIChatTemplate : ResourceDictionary
    {
        private bool _autoScroll = true;

        public AIChatTemplate()
        {
            InitializeComponent();
        }

        private void ChatScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            var scrollViewer = sender as ScrollViewer;
            if (scrollViewer == null) return;

            // Check if user manually scrolled up
            if (e.ExtentHeightChange == 0)
            {
                // User scrolled manually
                _autoScroll = scrollViewer.VerticalOffset == scrollViewer.ScrollableHeight;
            }

            // Auto-scroll to bottom when new content is added
            if (_autoScroll && e.ExtentHeightChange != 0)
            {
                scrollViewer.ScrollToVerticalOffset(scrollViewer.ExtentHeight);
            }
        }
    }
}
