# `gamerules.md` — the rule, in prose

This is the second of three surfaces the meld rule lives on. Source: `docs/gamerules.md` in the **UpAllNight** repo.

This file is the canonical written rule for the next contributor — the audience that has to learn the game from scratch and then implement scoring, validation, or a new feature without guessing what "second book of the same rank" means. Prose, not conditionals. No HTML, no `<strong>` tags, no validator branches. Just the rules as a board-game rulebook would describe them.

The two lines that changed when the meld rule was wrong:

- *"A 2nd book of the same rank may be of the **opposite** type from the first."* (Was: had to match.)
- *"Cards may be added to a **completed** book for additional scoring."* (Was: completed books accepted no further cards.)

Below is the full "Rules" section of `gamerules.md` as it sits today. The two changed lines are in the middle of the list.

```markdown
# Rules
* Natural books are seven cards of the same rank without using wild cards.
* Dirty books are seven cards of same rank created using wild cards.
* When a team is partnered then one person in the team will hold the books
  and both players contribute to the books.
* If a team makes a natural book, they can either just add cards to that
  book for scoring after closing the book or starting a new dirty book.
  The same is if they started with a dirty book, they can either add to
  the book for additional points or start a new natural book.
* A 2nd book of the same rank may be of the **opposite** type from the
  first (e.g. a closed natural 7s book + a new dirty 7s book), provided
  the new book independently satisfies its type rules — natural books
  contain only matching naturals, and a new dirty book must include at
  least one wild card.
* Cards (including extras of the same rank) may be added to a **completed**
  book for additional scoring. Wild cards cannot be added to a completed
  natural book — completing a natural book locks its type.
* While a natural book is still **incomplete** (fewer than 7 cards), a
  wild card may be played onto it to convert it into a dirty book — but
  only if the team does not already have a dirty book of that rank.
* After cards are dealt, 1 card is placed on the table to start the
  discard pile, the remaining undealt cards are set next to the discard
  pull for players to pull from.
* Similar to other card games, the dealer switches between players each
  new deal of the cards.
* Each player, starting with the player to the left of the dealer, either
  draws 2 cards or pulls up the top 5 cards from the discard pile.
  * If pulling from the discard pile the player must have at least 2
    cards of the top card suit and is able to start a book if no book
    is on the table for that team
    * Wild cards cannot be used to start this book
  * If pulling from the discard pile and the team already has a book
    started of the top card rank then the player only needs 1 card
    matching the top card
  * If a 3 is showing as the top card of the discard pile then the
    player cannot pull any cards
  * There must be at least 5 cards in the discard pile before the
    player is able to choose to the discard pile instead of drawing
    2 new cards from the fresh card pile
```

The doc is the quietest of the three surfaces when it's wrong. Contributors are rare and they usually check the code first anyway. Which is exactly the problem: a wrong doc rots silently for months, until someone tries to learn the game from it and implements the wrong rule.

Companion rules on the other two surfaces:

- `MeldService.ValidateMeld` (sample 01) — the validator.
- `RulesContent.razor` (sample 03) — the in-game rules modal.
