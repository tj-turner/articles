// Article 2 — "The Shared Foundation: Building an AI Library You'd Actually Reuse"
// A consuming service, in full. Authorize, build options, call, translate.
//
// This is the whole AI code path of the chat service. The partner-tool service
// differs by three lines — Streaming = false, WriteSkillsAllowed = false, and a
// buffering observer instead of the SSE one. The partner-agent service differs
// by setting InboundTrustOverride. None of them re-implements the loop.
//
// Two details that are easy to get wrong and matter:
//
//  1. The handler returns Results.Empty. The observer has been writing and
//     flushing to the response since the first token, so returning a value for
//     the framework to serialize would either append a JSON blob to the tail of
//     the event stream or throw ("headers are read-only, response has started").
//
//  2. The conversation-ownership check lives HERE, not in the library. Whether
//     this caller owns this conversation is a question about our data model. The
//     library can't answer it and shouldn't try — which is why the adapter has
//     four verbs and not three.

using SharedAi;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSharedAi(builder.Configuration);   // one extension method
builder.Services.AddScoped<IConversationAccess, ConversationAccess>();
var app = builder.Build();

app.MapPost("/conversations/{conversationId:guid}/turns", async (
    Guid                conversationId,
    TurnBody            body,
    IAgentOrchestrator  orchestrator,
    IConversationAccess access,
    HttpContext         http,
    CancellationToken   ct) =>
{
    var caller = http.User.ToCaller();

    // Not the library's call to make. 404 rather than 403 — don't confirm the
    // existence of a conversation this caller has no business knowing about.
    if (!await access.OwnsAsync(caller, conversationId, ct))
        return Results.NotFound();

    var options = new TurnOptions
    {
        Streaming          = true,   // tokens go out as they arrive
        WriteSkillsAllowed = true,   // via propose-confirm-execute
        OriginatingService = OriginatingService.Chat,
    };

    // The observer owns the response from here: it sets the SSE headers,
    // disables response buffering, heartbeats so intermediaries don't time the
    // connection out, and writes each token as it lands.
    var observer = new SseTurnObserver(http.Response);

    await orchestrator.RunTurnAsync(
        new TurnRequest(conversationId, body.Message, caller, body.IdempotencyKey),
        observer,
        options,
        ct);

    return Results.Empty;   // the body has already been written and flushed
})
.RequireAuthorization();

app.Run();

public sealed record TurnBody(string Message, Guid IdempotencyKey);

public interface IConversationAccess
{
    Task<bool> OwnsAsync(Caller caller, Guid conversationId, CancellationToken ct);
}
