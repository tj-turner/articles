// The reaper for the OTHER kind of orphan: a Waiting lobby nobody ever joined.
// A background service prunes them on a timer so dead lobbies don't pile up,
// then notifies the creator's personal group so their waiting room clears.
//
// The thing to notice: this only touches WAITING games (PruneStaleWaitingAsync).
// It deliberately leaves InProgress games alone. There are two kinds of orphan
// in a multiplayer game and they need opposite treatment:
//   - "Nobody joined"      -> reap it (this service).
//   - "Tab closed mid-game" -> give the player a way back in (see 02).
// Reaping the second kind would delete a live game out from under a player who
// just lost their connection. The fix for that orphan is resume, not cleanup.
//
// Trimmed from UpAllNight.Api.BackgroundServices.StaleGameSweepService.

public class StaleGameSweepService(
    IServiceScopeFactory scopeFactory,
    IHubContext<GameHub> hubContext,
    IConfiguration configuration,
    ILogger<StaleGameSweepService> logger) : BackgroundService
{
    private readonly TimeSpan _maxAge =
        TimeSpan.FromMinutes(configuration.GetValue("Lobby:StaleWaitingTimeoutMinutes", 15));
    private readonly TimeSpan _interval =
        TimeSpan.FromMinutes(configuration.GetValue("Lobby:StaleWaitingSweepIntervalMinutes", 5));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await SweepOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Stale game sweep tick failed."); }

            try { await Task.Delay(_interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task SweepOnceAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var gameService = scope.ServiceProvider.GetRequiredService<IGameService>();

        // Only Waiting games that never filled. InProgress games are never swept.
        var abandoned = await gameService.PruneStaleWaitingAsync(_maxAge, ct);
        if (abandoned.Count == 0) return;

        foreach (var game in abandoned)
        {
            await hubContext.Clients.Group($"user-{game.UserId}")
                .SendAsync("GameAbandoned",
                    new GameAbandonedPayload(game.GameId, "No one joined in time."), ct);
        }
    }
}
