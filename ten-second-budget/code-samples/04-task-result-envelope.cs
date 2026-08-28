// The result envelope.
//
// Three fields around the payload, and the middle one is the argument. Kind
// tells a renderer which component to reach for. Version tells it which shape
// of that component's props it is looking at. Payload is whatever the task
// produced.
//
// A result row is written once and read for as long as the conversation exists,
// so the rows in the table are a permanent record of every payload shape the
// system has ever emitted. Adding a column to a report next quarter does not get
// to break a card someone opens from a thread eighteen months old. Versioning
// the envelope is cheaper than migrating those rows, and much cheaper than the
// alternative where the renderer guesses from the payload's keys.

using System;
using System.Collections.Generic;

namespace Platform.Ai.Contracts.Tasks;

public sealed record TaskResultEnvelope<TPayload>(
    string Kind,
    string Version,
    TPayload Payload);

public sealed record StructuredReportPayload(
    string ReportTitle,
    IReadOnlyList<ReportColumn> Columns,
    IReadOnlyList<IReadOnlyList<string?>> Rows,
    string Narrative,
    IReadOnlyList<string> Caveats,
    // Both floors travel with the result. The renderer is not the right place
    // to work out what a reader is allowed to see, and by the time this row is
    // opened the turn that produced it is long gone.
    string ClassificationFloor,
    string ClassificationCeiling,
    IReadOnlyList<string> DataSources);

public sealed record ReportColumn(string Header, ColumnAlignment Alignment, string? Format);

public enum ColumnAlignment { Left, Right }
