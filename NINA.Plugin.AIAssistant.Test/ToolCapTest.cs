using FluentAssertions;
using NINA.Plugin.AIAssistant.AI;
using NINA.Plugin.AIAssistant.AI.MCP;
using NUnit.Framework;

namespace NINA.Plugin.AIAssistant.Test {

    /// <summary>
    /// OpenAI refuses more than 128 tools in one request ("array too long", seen live with the
    /// 193 the plugin carries). The cap has to be met by dropping the tools least likely to
    /// matter in a conversation - rescans, device listings, one-by-one disconnects - and never
    /// the ones the system prompt names or the user configured by hand.
    /// </summary>
    [TestFixture]
    public class ToolCapTest {

        private static MCPTool T(string name) => new MCPTool { Name = name };

        private static List<MCPTool> Many(string prefix, int count) =>
            Enumerable.Range(0, count).Select(i => T($"{prefix}{i}")).ToList();

        [Test]
        public void UnderTheCap_NothingChanges() {
            var tools = new List<MCPTool> { T("wiki_search"), T("nina_get_status"), T("nina_rescan_camera") };

            var kept = ToolCap.Apply(tools, 128, out var dropped);

            kept.Should().BeEquivalentTo(tools, o => o.WithStrictOrdering());
            dropped.Should().BeEmpty();
        }

        [Test]
        public void OverTheCap_DropsTheLowestPriorityGroupsFirst_AndStopsAsSoonAsItFits() {
            var tools = new List<MCPTool>();
            tools.AddRange(Many("nina_rescan_", 5));
            tools.AddRange(Many("nina_list_", 5).Select(t => T(t.Name + "_devices")));
            tools.AddRange(Many("nina_disconnect_", 5));
            tools.AddRange(Many("nina_get_", 10));

            // 25 tools, cap 17: the 5 rescans go, then 5 listings - 15 fits, disconnects stay.
            var kept = ToolCap.Apply(tools, 17, out var dropped);

            kept.Should().HaveCount(15);
            kept.Should().NotContain(t => t.Name.StartsWith("nina_rescan_"));
            kept.Should().NotContain(t => t.Name.EndsWith("_devices"));
            kept.Should().Contain(t => t.Name.StartsWith("nina_disconnect_"));
            dropped.Should().HaveCount(10);
        }

        [Test]
        public void DisconnectAll_SurvivesTheDisconnectCut() {
            var tools = Many("nina_disconnect_", 5);
            tools.Add(T("nina_disconnect_all_equipment"));
            tools.AddRange(Many("nina_get_", 3));

            var kept = ToolCap.Apply(tools, 5, out _);

            kept.Should().Contain(t => t.Name == "nina_disconnect_all_equipment");
            kept.Should().NotContain(t => t.Name == "nina_disconnect_0");
        }

        [Test]
        public void SurvivorsKeepTheirOriginalOrder() {
            var tools = new List<MCPTool> { T("wiki_index"), T("nina_rescan_camera"), T("nina_get_status"), T("nina_rescan_mount"), T("nina_capture_image") };

            var kept = ToolCap.Apply(tools, 3, out _);

            kept.Select(t => t.Name).Should().ContainInOrder("wiki_index", "nina_get_status", "nina_capture_image");
        }

        [Test]
        public void StillOverAfterEveryGroup_TrimsFromTheEnd_ButNeverTheProtectedOnes() {
            var tools = new List<MCPTool> { T("nina_get_status"), T("wiki_search") };
            tools.AddRange(Many("nina_foo_", 10));

            var kept = ToolCap.Apply(tools, 5, out var dropped);

            kept.Should().HaveCount(5);
            kept.Should().Contain(t => t.Name == "nina_get_status").And.Contain(t => t.Name == "wiki_search");
            dropped.Should().HaveCount(7);
        }

        [Test]
        public void ExternalTools_AreNeverDropped() {
            // Anything not from the built-in bridge was configured by the user on purpose.
            var tools = Many("nina_rescan_", 6);
            tools.Add(T("brave_search"));
            tools.Add(T("fetch"));

            var kept = ToolCap.Apply(tools, 4, out _);

            kept.Should().Contain(t => t.Name == "brave_search").And.Contain(t => t.Name == "fetch");
        }

        [Test]
        public void TheRealCatalogue_FitsUnderOpenAIsCap() {
            // The shipped tool list, as the bridge declares it, must come out at or under 128
            // without reaching the last-resort tail cut.
            var names = new NINAAdvancedAPIClient().GetAvailableTools().Select(t => t.Name).ToList();
            names.Count.Should().BeGreaterThan(128, "otherwise this test guards nothing");

            var kept = ToolCap.Apply(names.Select(T).ToList(), 128, out var dropped);

            kept.Count.Should().BeLessThanOrEqualTo(128);
            kept.Should().Contain(t => t.Name == "nina_get_status")
                .And.Contain(t => t.Name == "nina_connect_all_equipment")
                .And.Contain(t => t.Name == "nina_capture_image")
                .And.Contain(t => t.Name == "nina_slew_mount")
                .And.Contain(t => t.Name == "nina_show_profile")
                .And.Contain(t => t.Name == "wiki_search");
            dropped.Should().OnlyContain(n => ToolCap.IsInALowPriorityGroup(n), "the catalogue should fit without the tail cut");
        }
    }
}
