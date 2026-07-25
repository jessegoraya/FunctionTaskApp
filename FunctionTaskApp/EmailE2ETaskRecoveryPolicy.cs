using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Taslow.Task.Model;

namespace Taslow.Task.Function
{
    internal sealed record EmailE2ERecoveryCandidate(
        string GroupTaskId,
        string IdempotencyKey,
        DateTimeOffset CreatedAt);

    internal static class EmailE2ETaskRecoveryPolicy
    {
        private const string AgentIdentity = "TaslowEmailExtractionAgent";
        private const string SourceMarker = "sourceSystem=TaslowEmailExtractionAgent";
        private const string IdempotencyMarker = "idempotencyKey=";
        private static readonly TimeSpan MaximumWindow = TimeSpan.FromMinutes(2);

        internal static bool TryValidateWindow(
            string createdAfterValue,
            string createdBeforeValue,
            out DateTimeOffset createdAfter,
            out DateTimeOffset createdBefore,
            out string error)
        {
            const DateTimeStyles styles =
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;
            createdAfter = default;
            createdBefore = default;

            if (!DateTimeOffset.TryParse(
                    createdAfterValue,
                    CultureInfo.InvariantCulture,
                    styles,
                    out createdAfter)
                || !DateTimeOffset.TryParse(
                    createdBeforeValue,
                    CultureInfo.InvariantCulture,
                    styles,
                    out createdBefore))
            {
                error = "Valid createdAfter and createdBefore UTC timestamps are required.";
                return false;
            }

            if (createdBefore < createdAfter
                || createdBefore - createdAfter > MaximumWindow)
            {
                error = "The recovery window must be ordered and no longer than two minutes.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        internal static IReadOnlyList<EmailE2ERecoveryCandidate> FindCandidates(
            GroupTaskSet taskSet,
            DateTimeOffset createdAfter,
            DateTimeOffset createdBefore) =>
            (taskSet?.grouptask ?? new List<GroupTask>())
                .Where(task => string.Equals(
                    task.createdby,
                    AgentIdentity,
                    StringComparison.Ordinal))
                .Select(task => new
                {
                    Task = task,
                    IdempotencyKey = ExtractIdempotencyKey(task.groupetasknotes)
                })
                .Where(item =>
                    !string.IsNullOrWhiteSpace(item.Task.grouptaskid)
                    && !string.IsNullOrWhiteSpace(item.IdempotencyKey)
                    && ContainsExactMarker(item.Task.groupetasknotes, SourceMarker))
                .Select(item => new EmailE2ERecoveryCandidate(
                    item.Task.grouptaskid,
                    item.IdempotencyKey,
                    AsUtc(item.Task.createddate)))
                .Where(item =>
                    item.CreatedAt >= createdAfter
                    && item.CreatedAt <= createdBefore)
                .ToList();

        private static string ExtractIdempotencyKey(string notes)
        {
            var marker = (notes ?? string.Empty)
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .SingleOrDefault(value => value.StartsWith(
                    IdempotencyMarker,
                    StringComparison.Ordinal));
            var value = marker?[IdempotencyMarker.Length..] ?? string.Empty;
            return IsSha256(value) ? value : string.Empty;
        }

        private static bool ContainsExactMarker(string notes, string marker) =>
            (notes ?? string.Empty)
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(value => string.Equals(value, marker, StringComparison.Ordinal));

        private static bool IsSha256(string value) =>
            !string.IsNullOrWhiteSpace(value)
            && value.Length == 64
            && value.All(character =>
                character is >= '0' and <= '9'
                || character is >= 'a' and <= 'f');

        private static DateTimeOffset AsUtc(DateTime value)
        {
            var normalized = value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
                : value.ToUniversalTime();
            return new DateTimeOffset(normalized);
        }
    }
}
