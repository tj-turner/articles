// Why a larger page size came back as an empty table.
//
// An illustration, not a transcription of anybody's service. The shape: a
// projection that issues a round trip per item, awaits them all at once, and sits
// inside a catch that returns. Past some page size the fan-out exceeds what the
// database will admit, the underlying call throws, the catch eats it, the list is
// never appended to, and the caller is handed a successful response with nothing
// in it.
//
// The error, when anything lets it through:
//
//     Resource ID : 1. The request limit for the database is 90 and has been reached.
//
// Resource ID 1 is the worker limit. The message says "request" for backward
// compatibility, from when one request meant one worker, and that is the whole
// reason a page size is the wrong dial: you cannot compute a safe one from how
// wide your fan-out is, because the thing being counted is not your requests. The
// budget is also shared across every caller of the database rather than allowed
// per request, so a page size that is safe on a quiet afternoon fails on a busy
// one, and it is provisioned per environment, so a larger tier fails at a larger
// page rather than not failing.
//
// Bounding the fan-out stops the failure. It does not make the read fast. The fix
// that does is to stop asking per item at all.
//
// MIT licensed, like everything in this folder.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SharedAi.Reporting;

public interface IRecipientReader
{
    /// <summary>One round trip per payable. Cheap at fifty of them.</summary>
    Task<IReadOnlyList<PayableRow>> ReadRecipientsAsync(Guid payableId, CancellationToken ct);
}

public sealed class PayableProjection(IRecipientReader reader)
{
    /// <summary>The shape that fails, and fails as a success.</summary>
    public async Task<IReadOnlyList<PayableRow>> ProjectUnboundedAsync(
        IReadOnlyList<Guid> payableIds,
        CancellationToken ct)
    {
        try
        {
            // One task per payable, all started at once. Nothing bounds the fan-out,
            // so the ceiling is whatever the database will accept before it refuses.
            var fanOut = payableIds.Select(id => reader.ReadRecipientsAsync(id, ct));
            var pages = await Task.WhenAll(fanOut);

            return pages.SelectMany(page => page).ToList();
        }
        catch (Exception)
        {
            // Upstream this is an HTTP 200 with an empty list and no error flag set,
            // which is indistinguishable from a business that owes nothing.
            //
            // It also swallows OperationCanceledException, so a budget running out
            // mid-gather becomes a successful empty report rather than a timeout — in
            // a tier whose terminal state depends on that exception arriving.
            return [];
        }
    }

    /// <summary>The bounded version, and the three things the first one is missing.
    ///
    /// Concurrency is capped. Cancellation and failure reach the caller instead of
    /// being swallowed. And the walk stops on the first refusal rather than spending
    /// the rest of the page discovering the same ceiling again: Task.WhenAll does not
    /// short-circuit, so a fan-out built on it keeps calling a database that has
    /// already said no.
    ///
    /// What none of that fixes is the round trip per payable. Measured against the
    /// real endpoint, time was linear in the row count at roughly half a second a
    /// payable — a fan-out whose work was not overlapping. Capping it to a handful in
    /// flight makes that worse. The repair is to project the recipients in the query
    /// and have no fan-out to bound.</summary>
    public async Task<IReadOnlyList<PayableRow>> ProjectBoundedAsync(
        IReadOnlyList<Guid> payableIds,
        int maxInFlight,
        CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxInFlight, 1);

        // Indexed rather than appended, because the gather's coverage line is about
        // which rows these are. A shared list under a lock returns them in whatever
        // order they finished.
        var pages = new IReadOnlyList<PayableRow>[payableIds.Count];

        await Parallel.ForEachAsync(
            Enumerable.Range(0, payableIds.Count),
            new ParallelOptions { MaxDegreeOfParallelism = maxInFlight, CancellationToken = ct },
            async (i, token) => pages[i] = await reader.ReadRecipientsAsync(payableIds[i], token));

        return pages.Where(page => page is not null).SelectMany(page => page).ToList();
    }

    /// <summary>What the catch should have been. An HttpClient timeout surfaces as
    /// TaskCanceledException, which derives from OperationCanceledException, so
    /// without the filter a stalled dependency gets recorded as the task running out
    /// of wall clock. Two different faults wearing one reason. Only a canceled
    /// request token means the clock; everything else keeps its own message.</summary>
    public async Task<IReadOnlyList<PayableRow>> ProjectOrReportAsync(
        IReadOnlyList<Guid> payableIds,
        int maxInFlight,
        CancellationToken ct)
    {
        try
        {
            return await ProjectBoundedAsync(payableIds, maxInFlight, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;   // the wall clock, and the caller records it as that
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Recipient projection failed after {payableIds.Count} payables: {ex.Message}", ex);
        }
    }
}
