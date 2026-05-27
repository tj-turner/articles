// The entire real-time "API" for a multiplayer card game: four group methods
// and a personal per-user group. The server doesn't stream game state over this
// hub — it pushes "something changed, refetch" to a group, and the database
// stays the source of truth. SignalR is the doorbell, not the house.
//
// JWT auth is wired through SignalR via the access_token query string for paths
// starting with /hubs (configured in the JWT bearer options, not shown here).
//
// Trimmed from UpAllNight.Api.Hubs.GameHub.

[Authorize]
public class GameHub : Hub
{
    // Per-game group: everyone seated at one table. The server broadcasts
    // turn/meld/discard events here; clients react by fetching fresh state.
    public async Task JoinGame(Guid gameId) =>
        await Groups.AddToGroupAsync(Context.ConnectionId, gameId.ToString());

    public async Task LeaveGame(Guid gameId) =>
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, gameId.ToString());

    // Per-lobby group: people watching a game's waiting room fill up.
    public async Task JoinLobby(Guid gameId) =>
        await Groups.AddToGroupAsync(Context.ConnectionId, $"lobby-{gameId}");

    public async Task LeaveLobby(Guid gameId) =>
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"lobby-{gameId}");

    // Every connection joins a personal group keyed by user id, so the server
    // can reach a specific player directly — invites, friend requests, and the
    // "your stale lobby was abandoned" notification all target user-{id}.
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user-{userId}");
        await base.OnDisconnectedAsync(exception);
    }

    private Guid GetUserId() =>
        Context.User?.GetUserId()
            ?? throw new HubException("User is not authenticated.");
}
