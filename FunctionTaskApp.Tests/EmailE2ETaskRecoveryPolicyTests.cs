using System;
using System.Collections.Generic;
using Taslow.Task.Function;
using Taslow.Task.Model;
using Xunit;

namespace FunctionTaskApp.Tests
{
    public sealed class EmailE2ETaskRecoveryPolicyTests
    {
        private const string IdempotencyKey =
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        [Fact]
        public void FindCandidates_ReturnsOnlySourceVerifiedAgentTaskInsideWindow()
        {
            var createdAt = new DateTime(2026, 7, 25, 16, 35, 47, DateTimeKind.Utc);
            var taskSet = new GroupTaskSet
            {
                grouptask = new List<GroupTask>
                {
                    BuildTask("expected", createdAt),
                    BuildTask("human", createdAt, createdBy: "user@bloomsky.onmicrosoft.com"),
                    BuildTask("wrong-source", createdAt, sourceSystem: "SomethingElse"),
                    BuildTask("invalid-key", createdAt, idempotencyKey: "not-a-sha"),
                    BuildTask("outside-window", createdAt.AddMinutes(3))
                }
            };

            var result = EmailE2ETaskRecoveryPolicy.FindCandidates(
                taskSet,
                new DateTimeOffset(createdAt.AddSeconds(-30)),
                new DateTimeOffset(createdAt.AddSeconds(30)));

            var candidate = Assert.Single(result);
            Assert.Equal("expected", candidate.GroupTaskId);
            Assert.Equal(IdempotencyKey, candidate.IdempotencyKey);
            Assert.Equal(new DateTimeOffset(createdAt), candidate.CreatedAt);
        }

        [Theory]
        [InlineData("invalid", "2026-07-25T16:36:00Z")]
        [InlineData("2026-07-25T16:36:00Z", "2026-07-25T16:35:00Z")]
        [InlineData("2026-07-25T16:34:00Z", "2026-07-25T16:36:01Z")]
        public void TryValidateWindow_RejectsInvalidOrOverbroadWindow(
            string createdAfter,
            string createdBefore)
        {
            var valid = EmailE2ETaskRecoveryPolicy.TryValidateWindow(
                createdAfter,
                createdBefore,
                out _,
                out _,
                out var error);

            Assert.False(valid);
            Assert.False(string.IsNullOrWhiteSpace(error));
        }

        [Fact]
        public void TryValidateWindow_AcceptsTwoMinuteUtcWindow()
        {
            var valid = EmailE2ETaskRecoveryPolicy.TryValidateWindow(
                "2026-07-25T16:34:00Z",
                "2026-07-25T16:36:00Z",
                out var createdAfter,
                out var createdBefore,
                out var error);

            Assert.True(valid);
            Assert.Equal(TimeSpan.FromMinutes(2), createdBefore - createdAfter);
            Assert.Equal(string.Empty, error);
        }

        private static GroupTask BuildTask(
            string id,
            DateTime createdAt,
            string createdBy = "TaslowEmailExtractionAgent",
            string sourceSystem = "TaslowEmailExtractionAgent",
            string idempotencyKey = IdempotencyKey) =>
            new()
            {
                grouptaskid = id,
                createdby = createdBy,
                createddate = createdAt,
                groupetasknotes =
                    $"sourceSystem={sourceSystem}; idempotencyKey={idempotencyKey}"
            };
    }
}
