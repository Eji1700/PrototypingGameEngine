# Compile

Two players sitting opposite each other. Twelve protocols on the table, six of them drafted
1-2-2-1, three lines running across between the players, and a deck built out of whatever each
of them took. The fourth of the games here, and the engine it runs on is
[three directories up](../../../README.md).

```powershell
dotnet run -- compile play 2
dotnet run -- compile play 2 --rival easy   # the seat opposite, played by the program
dotnet run -- compile serve 2               # the same table in a browser, with buttons
```

**This game is unfinished, on purpose.** What is here is the table: the draft, the protocols
against the lines, the decks, the hands, and a card going onto a stack. What a card *does*, what
a player is trying to do with one, and how a game is won have not been written yet - so there
is no win condition in the code and there is no invented one either. See [What is not
here](#what-is-not-here), which is the honest list.

## Playing

Every command that is not about protocols or cards - `undo`, `redo`, `history`, `save`,
`notes`, `view`, `resign`, `restart`, `help`, `quit` - belongs to the engine and is
[documented there](../../../README.md). What this game adds is three verbs, one per stage.

| command | action |
| --- | --- |
| `fire` | take the Fire protocol at the draft |
| `draft fire` (`take`) | the same, the long way round |
| `water dark fire` | set your three against lines 1, 2 and 3, in that order |
| `arrange water dark fire` (`order`) | the same, the long way round |
| `fire-3 2` | play the card Fire-3 to line 2 |
| `play fire-3 2` | the same, the long way round |

The short forms exist because the stage already says which of the three a bare line could be,
and which one it is, is settled by how many words are in it: one word is a protocol, three are
an order, and a card and a number are a card going onto a line. The long forms are kept
because a record is written in them, and a record that read as a column of bare words would be
a record nobody could skim.

A card is written as a protocol, a dash and the number on it - `fire-3`. A protocol's six
cards carry six different numbers, so no two cards in a deck are the same card, and naming one
is naming a card rather than a place in a hand. That matters: `fire-3` is still `fire-3` after
the hand beside it has changed.

## How a game goes

**The draft.** Twelve protocols, no duplicates, and six picks: one to Player 1, two to Player
2, two back to Player 1, one back to Player 2. Three each. The shape is what pays for going
first - the player who chose first chooses again only after the other has had two - and the six
nobody took are gone for the rest of the game.

**The protocols.** Each player sets their three against the three lines, first for line one.
Both do it, and the lines they make are read across the table from each other: Player 1 might
have Water, Dark and Fire while Player 2 has Gravity, Metal and Light, and line two is then
Dark meeting Metal in the middle of the table.

**The deal.** Each deck is the eighteen cards of the three protocols that player drafted,
shuffled, and five are drawn.

**Play.** A card out of the hand, onto one of your three stacks. The turn passes.

```
                        Player 2
      Spirit-3  |  Metal-5   |     -
                |  Plague-4  |
     -----------+------------+-----------
       Line 1   |   Line 2   |   Line 3
       Spirit   |   Metal    |   Plague
       Water    |    Fire    |   Light
     -----------+------------+-----------
      Water-5   |   Fire-2   |     -
      Water-2   |            |
                        you
```

A stack grows away from the line it was played to, so the card most recently played is the one
nearest whoever played it - furthest up for them, furthest down for you. The two halves of the
board are therefore read in opposite directions, which is not a flourish: it is what the table
actually looks like from where the reader is sitting.

## Rules as implemented

- Two players, and exactly two, sitting opposite each other.
- Twelve protocols, no duplicates, drafted 1-2-2-1 - three each.
- Three lines, one per protocol drafted. Each player chooses which of theirs faces which line.
- A deck is 18 cards: three protocols of six. Five are drawn to open.
- A card is played from hand onto one of that player's three stacks, and the turn passes.
- A protocol already taken, a card not in hand, a line that is not there, and a move made at
  the wrong stage are all refused - and a refusal at the wrong stage says what the game *is*
  asking for, which is the thing that helps.
- `resign` gives the game up at any of the three stages, and writes it down.

**What is hidden, and what is not.** A hand is hidden; nothing else is. The draft is announced,
because a protocol taken is taken from both of them, and every stack is on the table. So there
is no `Knowledge` file here: nothing the game *says* is a secret, and `SeenBy` answers the same
for everybody. What one player may know about the other's cards is three counts - deck,
discard and hand - and those are on the screen precisely so that what the cards *are* need not
be.

## What is not here

Written down rather than left to be discovered:

- **What a card does.** A card is a protocol and a number. [Cards.fs](Rules/Cards.fs) is where
  an effect goes, and it is one field on one record - the six numbers are the placeholder,
  and they are deliberately the whole of it.
- **How a game is won.** There is none. `Ending` has one case and it is somebody walking away.
- **Drawing.** Nothing is drawn after the opening five. A deck runs down as it is played and
  stops there.
- **Discarding.** The pile exists, is counted on the screen, and nothing puts anything in it
  yet.
- **Face-down cards**, if this game is to have them.
- **A machine worth playing.** There is one, it is legal, and it is random - because a machine
  that plays *better* needs something to be better at, and that is the win condition above.

The invariant the tests hold to in the meantime is the one this game has before any of that is
settled: **eighteen cards each, wherever they are.** Deck plus hand plus discard plus everything
on the table is 18, and no card is in two places at once. Any rule added later could break that
without anybody noticing, which is exactly why it is checked now.

## The files

Twelve, in the shape every game here has: `Rules` is how it is played and contains no English
and nothing from the table layer; `Reading` is how it is read.

| File | Role |
| --- | --- |
| [Protocols.fs](Rules/Protocols.fs) | The twelve, and the words for them |
| [Cards.fs](Rules/Cards.fs) | A card, a protocol's six, and a deck shuffled out of three of them |
| [Field.fs](Rules/Field.fs) | The lines, one player's half of the table, and both halves together |
| [Drafting.fs](Rules/Drafting.fs) | Whose pick it is, which is a list of six and nothing else |
| [Session.fs](Rules/Session.fs) | The three stages, and whose turn it is at each of them |
| [Turn.fs](Rules/Turn.fs) | `Move`, and how a turn goes at each stage |
| [Words.fs](Rules/Words.fs) | Every string a player reads |
| [Rival.fs](Rules/Rival.fs) | A seat played by the program, through all three stages |
| [Ink.fs](Reading/Ink.fs) | Two colours: one per side of the table |
| [Parse.fs](Reading/Parse.fs) | Three verbs, each with a short form |
| [Render.fs](Reading/Render.fs) | Every screen described once as a [`Scene`](../../../README.md#a-screen-described-once), which `Readers` then draws three ways |
| [Offer.fs](Offer.fs) | Both seams filled in |

**What this game leant on that the others did not** is the stage. A game of this is three games
in a row - a draft, a laying-out, then play - with different moves and three different senses
of whose turn it is: the draft has an order of its own, the arranging goes round once, and play
alternates. All of that is `Session.active` answering one question three ways, and none of it
needed a line changing above the game. The engine asks *whose turn it is*; it has never asked
why.

The other thing worth noting is that `Doing` exists. A refusal for the wrong stage has to name
the stage the game is actually in - a player who typed a card at the draft wants to be told
what is wanted now - but a refusal carrying the whole `Stage` would carry the pool and the
ending with it, which is a notice quoting the position back at itself. So the stage is said
twice: once in full for the rules, and once small enough to travel in something a player reads.
