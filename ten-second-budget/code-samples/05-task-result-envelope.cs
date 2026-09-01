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
// to break a card someone opens from a thread a year old. Versioning the
// envelope costs a field. Not versioning it means either migrating every stored
// payload later, or teaching the renderer to sniff keys and guess.
//
// Version is an int, not a string. The renderer compares it to decide whether a
// payload is newer than it understands, and string ordering gets that wrong the
// moment there is a version 10: "10" sorts before "2".

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SharedAi.Contracts.Tasks;

public sealed record TaskResultEnvelope<TPayload>(
    string Kind,
    int Version,
    TPayload Payload);

public sealed record StructuredReportPayload(
    string ReportTitle,
    IReadOnlyList<ReportColumn> Columns,
    // Keyed by ReportColumn.Name rather than positional. Positional rows are the
    // shape most hostile to exactly the additive change the version field
    // exists to survive - insert a column and every row silently shifts.
    IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows,
    string Narrative,
    IReadOnlyList<string> Caveats,
    // Two values with different provenances, and it is worth keeping them
    // apart. The floor is a statement of intent, carried from the turn that
    // asked. The ceiling is a statement about the bytes, computed from the
    // composed result by the only thing that ever saw it whole. Effective
    // classification is the higher of the two, which is why they are an ordered
    // enum and not the strings they arrived as. Sorted as text, "Public" lands
    // after "Confidential", so a public floor and a confidential ceiling resolve
    // to public - silently, and in the direction that declassifies.
    Classification ClassificationFloor,
    Classification ClassificationCeiling,
    IReadOnlyList<string> DataSources);

// Name is the key the rows are dictionaried by; Header is what gets drawn. They
// start out identical and the reason to separate them is that Header is the one
// that gets reworded, localized, or duplicated across two columns - and if the
// rows are keyed by a display string, renaming a heading silently empties a
// column in every stored report.
public sealed record ReportColumn(
    string Name, string Header, ColumnAlignment Alignment, string? Format);

public enum ColumnAlignment { Left, Right }

// Stored by name, compared by value, and those are two different arguments.
//
// Comparison wants an ordered enum: effective classification is the higher of
// floor and ceiling, and max() over text gets that wrong (see above).
//
// Storage wants the name, because the numbers are not stable across the system.
// This enum grew Confidential and Secret after the version that shipped with
// { Public, Internal, Restricted }, which moved Restricted from 2 to 3. Any
// payload serialized numerically under the old set now reads back one level
// lower - silently, and in the declassifying direction. The envelope's Version
// field cannot catch it, because inserting an enum member is not a payload
// shape change and nothing bumps.
[JsonConverter(typeof(JsonStringEnumConverter<Classification>))]
public enum Classification
{
    Public = 0,
    Internal = 1,
    Confidential = 2,
    Restricted = 3,
    Secret = 4,
}
