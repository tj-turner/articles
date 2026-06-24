// ============================================================
//  MeldService.ValidateMeld — the rule, in code.
//  Source: src/UpAllNight.Services/Game/MeldService.cs
// ============================================================
//
//  This is the first of three surfaces the meld rule lives on.
//  The runtime calls this every time a player tries to place
//  cards on the table. It says yes or no, in real time, with
//  no commentary.
//
//  What changed when the original rule turned out to be wrong:
//   1. The `targetBook` parameter was added so the validator
//      can distinguish "creating a new book" from "extending
//      an existing one." The extension path accepts completed
//      books — that's the lay-off case the old rule refused.
//   2. A team may now hold a natural + dirty book of the same
//      rank (opposite types). The original rule required them
//      to match. The new rule requires opposite, with a guard
//      that two dirty books of the same rank are forbidden
//      because type is inferred from wild presence, so two
//      dirty books would be indistinguishable.
//
//  The companion rule on the two other surfaces:
//   - docs/gamerules.md (sample 02)
//   - RulesContent.razor (sample 03)
//
//  Change one, you've drifted. Change all three, you've shipped.
// ============================================================

using UpAllNight.Services.Interfaces.Services;
using UpAllNight.Services.Models;
using UpAllNight.Services.Models.Enums;

namespace UpAllNight.Services.GamePlay;

public class MeldService : IMeldService
{
    private static readonly int[] RoundMinimums = [50, 90, 120, 150];

    /// <summary>
    /// Validates whether the given cards form a valid meld for the specified rank and round.
    /// Rules:
    ///   - 3s are never meldable.
    ///   - All natural (non-wild) cards must match the target rank.
    ///   - When CREATING a new book (targetBook == null): at least 3 cards, wilds ≤ naturals.
    ///   - When EXTENDING an existing book (targetBook != null): at least 1 card; rank must match
    ///     the target's rank. Completed books may still be extended for additional scoring.
    ///     Adding a wild to a Natural book converts it to Dirty — allowed only while the book is
    ///     still incomplete AND the team has no existing Dirty book of the same rank.
    ///   - Players MAY start a second book of the same rank with the opposite type, provided that
    ///     book independently satisfies natural/dirty rules (a Dirty book naturally requires ≥1 wild
    ///     because type is inferred from wild presence).
    /// </summary>
    public bool ValidateMeld(
        List<PlayingCard> cards,
        CardRank rank,
        int roundNumber,
        List<Book> existingBooks,
        Book? targetBook = null)
    {
        if (rank == CardRank.Three)
            return false;

        var naturalCards = cards.Where(c => !c.IsWild && !c.IsJoker).ToList();
        var wildCards = cards.Where(c => c.IsWild || c.IsJoker).ToList();

        // All natural cards must match the target rank.
        if (naturalCards.Any(c => c.Rank != rank))
            return false;

        // ----- Extending an existing book (lay-off case). -----
        if (targetBook is not null)
        {
            if (cards.Count < 1)
                return false;

            if (targetBook.Rank != rank)
                return false;

            // Adding a wild to a Natural book converts it to Dirty.
            // Allowed only while the book is still incomplete AND the
            // team doesn't already own a Dirty book of this rank
            // (otherwise the conversion would create a duplicate-type pair).
            if (targetBook.BookType == BookType.Natural && wildCards.Count > 0)
            {
                if (targetBook.IsComplete)
                    return false;

                var hasDirtyOfSameRank = existingBooks.Any(b =>
                    b.Rank == rank && b.BookType == BookType.Dirty);
                if (hasDirtyOfSameRank)
                    return false;
            }

            return true;
        }

        // ----- Creating a new book. -----
        if (cards.Count < 3)
            return false;

        // Cannot have more wild cards than natural cards
        // (also forces ≥1 natural to anchor the rank).
        if (wildCards.Count > naturalCards.Count)
            return false;

        return true;
    }
}
