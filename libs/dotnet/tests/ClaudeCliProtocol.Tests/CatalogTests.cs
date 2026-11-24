using System;
using Xunit;

namespace ClaudeCliProtocol.Tests
{
    public class CatalogTests
    {
        [Fact]
        public void The_renamed_types_keep_their_shape()
        {
            var command = new SlashCommand("review", "Review a pull request", "<pr>");
            Assert.Equal("review", command.Name);
            Assert.Equal("Review a pull request", command.Description);
            Assert.Equal("<pr>", command.ArgumentHint);

            var model = new ModelOption("opus", "claude-opus-5-5", "Opus 5.5", "For complex work", new[] { "low", "high" });
            Assert.Equal("opus", model.Value);
            Assert.Equal("claude-opus-5-5", model.ResolvedModel);
            Assert.Equal("Opus 5.5", model.DisplayName);
            Assert.Equal(new[] { "low", "high" }, model.EffortLevels);
        }

        [Fact]
        public void A_snapshot_of_the_limits_says_so()
        {
            Assert.False(new UsageLimits(Array.Empty<UsageWindow>()).IsSnapshot);
            Assert.True(new UsageLimits(Array.Empty<UsageWindow>()) { IsSnapshot = true }.IsSnapshot);
        }
    }
}
