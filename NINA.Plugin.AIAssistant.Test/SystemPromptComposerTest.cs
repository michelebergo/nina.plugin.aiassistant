using FluentAssertions;
using NINA.Plugin.AIAssistant.AI;
using NUnit.Framework;

namespace NINA.Plugin.AIAssistant.Test {

    /// <summary>
    /// Every provider resolves its own system prompt, and one of them (Anthropic) does so at
    /// four separate points. The composer is the single place that decides how the profile
    /// block joins that prompt, which is why it has to be safe to call more than once on the
    /// same text.
    /// </summary>
    [TestFixture]
    public class SystemPromptComposerTest {

        private const string Base = "You are an expert astrophotography assistant.";
        private const string Block = "ACTIVE NINA PROFILE \"Backyard\":\n  Site: 41.90N 12.50E";

        [Test]
        public void NoProfileBlock_LeavesThePromptAlone() {
            SystemPromptComposer.Compose(Base, null).Should().Be(Base);
            SystemPromptComposer.Compose(Base, "").Should().Be(Base);
            SystemPromptComposer.Compose(Base, "   ").Should().Be(Base);
        }

        [Test]
        public void NoPrompt_YieldsTheBlockOnItsOwn() {
            SystemPromptComposer.Compose(null, Block).Should().Be(Block);
            SystemPromptComposer.Compose("", Block).Should().Be(Block);
        }

        [Test]
        public void NothingAtAll_YieldsNothing() {
            SystemPromptComposer.Compose(null, null).Should().BeNull();
        }

        [Test]
        public void BothPresent_TheBlockFollowsThePrompt() {
            var composed = SystemPromptComposer.Compose(Base, Block)!;

            composed.Should().StartWith(Base);
            composed.Should().EndWith(Block);
            composed.Should().Contain("\n\n");
        }

        [Test]
        public void ComposingTwice_DoesNotRepeatTheBlock() {
            // A retry, or a provider that resolves its prompt at several points, must not end
            // up stating the rig twice: a model reading two rigs has no way to tell which one
            // it is pointed at, and the duplicate is charged for on every request.
            var once = SystemPromptComposer.Compose(Base, Block);
            var twice = SystemPromptComposer.Compose(once, Block);

            twice.Should().Be(once);
        }
    }
}
