using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Common;
using StarPlayer.Simulation.Randomness;

namespace StarPlayer.Simulation.Baseball;

/// <summary>
/// Phase 3-F F-A: the one public way to BEGIN a game's authority, for whoever owns the game (the headless harness today; a real runner later). The aggregate and the cursor are built
/// only inside Simulation (<see cref="AuthoritativeGame"/> has no public factory, <see cref="GameCursor"/> no public constructor), and nothing outside could start a new game
/// without reaching their internals. This adds no way to forge one: the caller supplies only a <see cref="GameStart"/> and a master seed, and the state is made HERE by the real
/// <see cref="GameReducer.Start"/> (its rules, execution-contract and lineup validation), the RNG context key is the game id by construction (so the game-id / RNG binding of
/// <see cref="AuthoritativeGame"/> holds), and the result is a new game: sequence 0, revision 0, empty receipt history, no last commit. A restored or mid-game state can still not be
/// supplied. Nothing is committed, drawn or persisted here, and the commit / compare-and-swap path is untouched.
/// </summary>
public static class GameStarter
{
    public static Result<GameCursor> Begin(GameStart? start, ulong masterSeed)
    {
        var state = GameReducer.Start(start);
        if (!state.IsSuccess)
            return Result<GameCursor>.Failure(state.Error!.Value);
        var random = GameRandom.Create(masterSeed, state.Value.GameId);
        if (!random.IsSuccess)
            return Result<GameCursor>.Failure(random.Error!.Value);
        var game = AuthoritativeGame.StartNew(state.Value, random.Value.Snapshot());
        return game.IsSuccess ? Result<GameCursor>.Success(new GameCursor(game.Value)) : Result<GameCursor>.Failure(game.Error!.Value);
    }
}
