// The gather that fed a structured report off the transactional paging read.
//
// Both numbers here were sized against that read and belong to it. Fifty rows a
// page was the page size the source could serve, pinned there by the per-payable
// fan-out in 02-unbounded-fanout.cs. Eight hundred rows was a forecast of the
// clock, calibrated once against a measured page time, in one environment — which
// is why the gather also carries a deadline and treats that as the authority when
// a page comes back slower than the forecast.
//
// The part worth copying is not the paging. It is that the result claims
// completeness only against a number the gather actually reached, and that every
// way out of the loop says which one it was. The first version of this file
// derived completeness from how the loop exited, and a single de-duplicated row
// turned a truncated read into a whole answer.
//
// MIT licensed, like everything in this folder.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace SharedAi.Reporting;

/// <summary>One payable as one recipient sees it. The grain matters: a payable
/// spanning several recipients is several rows, so amounts at this grain
/// partition and sum, and a payable count is a count of distinct payable ids.</summary>
public sealed record PayableRow(Guid PayableId, Guid RecipientId, decimal RecipientAmount, DateTime DueUtc);

/// <summary>A gather and what it is honest about: how much of the source it got.
/// <c>Coverage</c> is null if and only if <c>Complete</c> is true.</summary>
public sealed record GatherResult(
    IReadOnlyList<PayableRow> Rows,
    bool Complete,
    int? SourceCount,
    string? Coverage);

public interface IPayablesReader
{
    Task<IReadOnlyList<PayableRow>> ReadPageAsync(Guid businessId, int skip, int take, CancellationToken ct);

    /// <summary>The source's own count, where it supplies one. It is the denominator
    /// a truncated gather needs, and the only thing that can tell "no payables" apart
    /// from "no rows came back."</summary>
    Task<int?> CountAsync(Guid businessId, CancellationToken ct);
}

public sealed class PagedPayablesGather(IPayablesReader reader, TimeProvider? clock = null)
{
    public static readonly int PageSize = 50;

    /// <summary>The cap on the answer, not on the fetching: a call that stops here
    /// returns a cursor and HasMore, and the report says on its face that it does not
    /// cover everything. Nobody measured 800. It stands in for three separate
    /// pressures — rows held in memory, the size of the serialized result, and what
    /// the results table will store — so the first of the three to actually hurt is
    /// the one that should set it. The warehouse read has its own, different cap
    /// (@MaxRows, 2000) for the same three reasons and with no more evidence.</summary>
    public static readonly int RowCeiling = 800;

    /// <summary>What a page cost when the ceiling was calibrated. A forecast, not a
    /// constant: measured from one place against one environment, and paging deeper
    /// into a result set tends to get slower rather than faster.</summary>
    public static readonly TimeSpan ForecastPageDuration = TimeSpan.FromSeconds(23);

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <param name="gatherDeadlineUtc">When the gathering has to stop — not when the
    /// task runs out of budget. The caller subtracts whatever composing and writing
    /// the result will need, because a gather that spends the budget down to its last
    /// page hands a complete row set to a composer with no time to compose.</param>
    public async Task<GatherResult> GatherAsync(
        Guid businessId,
        DateTimeOffset gatherDeadlineUtc,
        CancellationToken ct)
    {
        // One round trip before the pages, for the denominator. Worth counting when
        // the budget is the thing under discussion: seventeen calls, not sixteen.
        var sourceCount = await reader.CountAsync(businessId, ct);

        var rows = new List<PayableRow>(RowCeiling);
        var seen = new HashSet<(Guid PayableId, Guid RecipientId)>();
        var reachedEnd = false;
        var skip = 0;

        while (skip < RowCeiling)
        {
            // When the next page will not fit, the clock wins over the row ceiling.
            // Without this the gather sails past the budget its ceiling was derived
            // from, on any afternoon slower than the one it was measured on.
            if (_clock.GetUtcNow() + ForecastPageDuration > gatherDeadlineUtc)
            {
                break;
            }

            var page = await reader.ReadPageAsync(businessId, skip, PageSize, ct);

            if (page.Count == 0)
            {
                // Nothing at all, against a count that says there is something, means
                // the read failed rather than that the business owes nothing. That is
                // the one case worth refusing outright.
                if (rows.Count == 0 && sourceCount is > 0)
                {
                    throw new InvalidOperationException(
                        $"Read returned no rows for a business holding {sourceCount} payables.");
                }

                // Any other empty page ends the walk. Whether that was the end of the
                // data is decided below, against the count — not here, against the
                // shape of the loop.
                reachedEnd = true;
                break;
            }

            foreach (var row in page)
            {
                // De-duplicated on the full grain. Keying on the payable alone drops
                // every recipient after the first, which silently undercounts the
                // money. The overlap itself is expected: the source's ordering has no
                // unique tiebreaker, so the same row can appear in two windows.
                if (seen.Add((row.PayableId, row.RecipientId)))
                {
                    rows.Add(row);
                }
            }

            // A short page is the only end-of-set signal this source offers, and it is
            // not a dependable one: rows can be dropped after the page has been cut,
            // so a full page's worth of slots can come back partly empty.
            if (page.Count < PageSize)
            {
                reachedEnd = true;
                break;
            }

            skip += PageSize;
        }

        // Completeness is claimed only against a number the gather reached. With no
        // count to check against, reaching the end of the pages is the best evidence
        // available and an empty result is no evidence at all.
        var complete = sourceCount is int total
            ? rows.Count >= total
            : reachedEnd && rows.Count > 0;

        var denominator = sourceCount?.ToString(CultureInfo.InvariantCulture) ?? "an unknown number of";

        return new GatherResult(
            Rows: rows,
            Complete: complete,
            SourceCount: sourceCount,
            // No claim about ordering: overlapping windows over an unstable sort
            // cannot promise these are the earliest rows, only that they are rows.
            Coverage: complete ? null : $"{rows.Count} of {denominator} payables.");
    }
}
