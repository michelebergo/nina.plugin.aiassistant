using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using FluentAssertions;
using NUnit.Framework;

namespace NINA.Plugin.AIAssistant.Test {

    /// <summary>
    /// The options page as WPF builds it from Options.xaml, exercised the way the code-behind
    /// uses it: every Test/Validate button must reach its result line through
    /// <see cref="OptionsTree.FindResultTextBlock"/>, the tabs must be the setup steps in order,
    /// and leaving a tab and coming back must hand the handlers the same controls. Bindings have
    /// no DataContext here and stay silent; what is tested is the tree, not the data.
    /// </summary>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class OptionsPageTest {

        private FrameworkElement _page = null!;
        private TabControl _tabs = null!;

        [SetUp]
        public void LoadPage() {
            // pack:// URIs resolve only once an Application exists in the process.
            if (Application.Current == null) _ = new Application();
            var dictionary = new Options();
            var template = (DataTemplate)dictionary["AI Assistant_Options"];
            _page = (FrameworkElement)template.LoadContent();
            _tabs = (TabControl)_page;
        }

        [Test]
        public void TabsAreTheSetupStepsInOrder() {
            _tabs.Items.Cast<TabItem>().Select(t => t.Header.ToString())
                .Should().Equal("Provider", "Equipment control", "Your rig", "Advanced", "About");
        }

        [Test]
        public void EveryNamedControlExistsOnce() {
            var names = Descendants(_page).OfType<FrameworkElement>().Select(e => e.Name).Where(n => !string.IsNullOrEmpty(n)).ToList();
            names.Should().OnlyHaveUniqueItems();
            names.Should().Contain(new[] {
                "ProviderSelector", "ProviderStatusText",
                "OllamaModelComboBox", "GoogleModelComboBox", "AnthropicModelComboBox", "OpenAIModelComboBox", "MistralModelComboBox",
                "MCPTestResult", "ExternalMCPServersBox", "UsefulServersCombo", "ExternalMCPValidateResult" });
        }

        [TestCase("GitHubTestResult", "Test")]
        [TestCase("OllamaTestResult", "Test")]
        [TestCase("GoogleTestResult", "Test")]
        [TestCase("AnthropicTestResult", "Test")]
        [TestCase("OpenAITestResult", "Test")]
        [TestCase("MistralTestResult", "Test")]
        [TestCase("MCPTestResult", "Test connection")]
        [TestCase("ExternalMCPValidateResult", "Validate")]
        [TestCase("ExternalMCPValidateResult", "Add")]
        [TestCase("ExternalMCPValidateResult", "Reset to default")]
        public void TheButtonReachesItsOwnResultLine(string resultName, string buttonContent) {
            var result = Named<TextBlock>(resultName);
            var buttons = Descendants(_page).OfType<Button>().Where(b => b.Content?.ToString() == buttonContent).ToList();

            // Several providers have a "Test" button: exactly one of them must land on this line,
            // and the others must land somewhere else - never on a neighbour's line.
            var reaching = buttons.Where(b => ReferenceEquals(OptionsTree.FindResultTextBlock(resultName, b), result)).ToList();
            reaching.Should().ContainSingle($"one '{buttonContent}' button must own {resultName}");
        }

        [Test]
        public void TheProviderSelectorReachesTheStatusLine() {
            var selector = Named<ComboBox>("ProviderSelector");
            OptionsTree.FindResultTextBlock("ProviderStatusText", selector).Should().BeSameAs(Named<TextBlock>("ProviderStatusText"));
        }

        [Test]
        public void EachProviderPanelIsOnTheProviderTab_AndTheToolsOnAdvanced() {
            TabOf(Named<ComboBox>("OpenAIModelComboBox")).Header.Should().Be("Provider");
            TabOf(Named<ComboBox>("OllamaModelComboBox")).Header.Should().Be("Provider");
            TabOf(Named<TextBlock>("MCPTestResult")).Header.Should().Be("Equipment control");
            TabOf(Named<TextBox>("ExternalMCPServersBox")).Header.Should().Be("Advanced");
        }

        [Test]
        public void LeavingATabAndComingBackKeepsTheSameControls() {
            var selector = Named<ComboBox>("ProviderSelector");
            var json = Named<TextBox>("ExternalMCPServersBox");

            _tabs.SelectedIndex = 3;
            _tabs.SelectedIndex = 4;
            _tabs.SelectedIndex = 0;

            Named<ComboBox>("ProviderSelector").Should().BeSameAs(selector);
            Named<TextBox>("ExternalMCPServersBox").Should().BeSameAs(json);
        }

        private T Named<T>(string name) where T : FrameworkElement =>
            Descendants(_page).OfType<T>().Single(e => e.Name == name);

        private TabItem TabOf(FrameworkElement element) {
            DependencyObject? node = element;
            while (node != null && node is not TabItem) node = LogicalTreeHelper.GetParent(node);
            return (TabItem)(node ?? throw new InvalidOperationException($"{element.Name} is on no tab"));
        }

        private static IEnumerable<DependencyObject> Descendants(DependencyObject root) {
            foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()) {
                yield return child;
                foreach (var grandChild in Descendants(child)) yield return grandChild;
            }
        }
    }
}
