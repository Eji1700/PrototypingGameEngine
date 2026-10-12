# How to play the eight games

## Sitting down

Every game starts the same way and takes the same table commands, so learn these once.

1. From a clone of the repository, run `dotnet run -- <game> play <n>`, where `<n>` is how many are playing. For example `dotnet run -- tictactoe play 2`.
2. Add `--rival hard` to give the seat after yours to the machine (`easy`, `medium` or `hard`). Add `--seed 42` to get the same deal again.
3. Run `dotnet run -- <game>` with nothing after it for a menu that asks the same questions.

At the prompt, type a move in the game's own words, or one of these:

| Type | What happens |
| --- | --- |
| `help` | every command, at length |
| `undo`, `redo` | walk the game back and forward |
| `history` | the record so far |
| `resign` | give the game up |
| `restart` | a fresh game for the same players |
| `quit` | leave; the game is saved and `replay` takes it up again |

The game names are `turncoats`, `tictactoe`, `diplomacy`, `compile`, `life`, `snake`, `cascade` and `warband`.

## Turncoats

Two to five play, with a bag of eight stones each in three colours, on a map of fourteen regions. You do not own a colour. You win by holding the most stones of whichever colour ends up ruling the board. Start with `dotnet run -- turncoats play 3`.

**A turn** is one of four actions, and there is no passing.

| Type | What happens |
| --- | --- |
| `recruit red 3` | a red stone from your bag goes into region 3 |
| `battle green 2 blue` | a green stone goes into the Axe, and a blue stone is driven out of region 2 |
| `march blue 4 5` | a blue stone goes into the Flag, and a blue stone moves from region 4 to 5 |
| `negotiate`, then `return red` | draw a stone from the reserve into your bag, then hand one back |

Colours are `red`, `blue` and `green`, or `r`, `b` and `g`. Regions are their numbers on the map. `rule 7` says who rules region 7 and why, without moving anything.

**Ruling a region.** The colour with most stones there rules it. Ties go to the colour with more stones in the Axe, then the Flag.

**The end.** The game ends when every player in turn negotiates instead of playing a stone, or when every bag is empty. The colour ruling the most land carries the board. The player holding the most stones of that colour still in their bag wins. If every bag was played out, nobody wins.

## Noughts and crosses

Two play on nine squares, crosses first, and nothing is hidden. Start with `dotnet run -- tictactoe play 2`, or `dotnet run -- tictactoe play 2 --rival hard` for a machine that cannot be beaten.

**A turn** is one mark on a free square, named by its number, 1 to 9, left to right and top to bottom. Type `5` to take the middle. `place 5` means the same. A square that is taken is refused and the turn does not pass.

**Winning.** Three of your mark in a row, down a column, or corner to corner wins on the mark that makes the line. A full board with no line is a draw.

## Diplomacy

Seven powers, thirty-four supply centres, no dice, and everybody writes at once. Start with `dotnet run -- diplomacy play 7`; seats you cannot fill take `--rival easy` each. The first power to hold eighteen centres at a winter count wins.

**A year** is Spring, Autumn and Winter, with a retreats phase after a season where a unit was beaten out. In a season every power writes one order per unit and then types `commit`. Nobody sees anybody's orders until the last power has committed, then the whole phase resolves at once.

**An order** names your unit by the province it stands in. Provinces are the three-letter codes on the map, such as `vie`, `tri` and `nth`.

| Type | What it orders |
| --- | --- |
| `vie hold` | the unit in Vienna stays put, which is what an unordered unit does anyway |
| `vie - tri` | it moves to Trieste |
| `bud s vie` | Budapest supports Vienna holding |
| `bud s vie - tri` | Budapest supports Vienna's move to Trieste |
| `nth c lon - bel` | the fleet in the North Sea convoys the army in London to Belgium |
| `cancel vie` | take Vienna's order back |
| `commit` | seal your orders and pass the prompt on |

A move succeeds when it is stronger than what holds the province and than every other move into it. Equal strengths bounce. A beaten unit retreats with `vie - tri` in the retreats phase, or `disband vie`. In winter, `build a vie` or `build f tri` adds a unit for each centre you hold over your unit count, and `disband` removes one for each you are short.

`borders vie`, `where mun` and `orders` answer questions on the spot without costing anything. `press france leave Trieste alone` sends a word to one power, and `press all` to the table.

## Compile

Two play, each with a deck of eighteen cards from three protocols, across three lines. Compile all three of your protocols and you win. Start with `dotnet run -- compile play 2`.

**The draft.** Fifteen protocols are on offer and six are picked, 1-2-2-1. Type a protocol's name to take it: `fire`. Then set your three against lines 1, 2 and 3 with `water darkness fire`. Your opponent sees your order only when both are in.

**A turn** is one action.

| Type | What happens |
| --- | --- |
| `fire-3 2` | plays Fire-3 face up on line 2, worth its number, and its text runs |
| `fire-3 2 down` | plays it face down on any line, worth 2, saying nothing |
| `refresh` | your whole hand to the discard, and five new cards |

A card goes face up only on a line its protocol sits on, yours or theirs. A card is its protocol, a dash and its number.

**Compiling.** At the start of your turn, every line where you have 10 or more and strictly more than your opponent compiles. The protocol facing it turns over, and every card on that line goes to its owner's discard. A line that will compile reads `ready` the turn before, which is the one turn they have to answer.

Cards ask questions as they run. Answer with a card name such as `fire-3`, a line number such as `2`, `yes` or `no`, or `first` or `second`. `what fire-3` reads a card, `peek` shows your own face-down cards, and `pile` lists what the game still has to do.

## Life

Conway's Game of Life for one, on a board of 26 columns by 16 rows whose edges are joined, so the left meets the right and the top meets the bottom. There is no winning. You draw, run the rule and watch. Start with `dotnet run -- life play 1`.

**The rule.** A living cell with two or three living neighbours lives on. An empty cell with exactly three comes alive. Everything else is empty next generation.

The board is dealt as a random soup, about three cells in ten alive, and starts running at speed 5 of 9. A beat of the clock is one generation.

| Type or press | What happens |
| --- | --- |
| `p` | run the rule, or stop it; `run`, `start` and `stop` at the prompt |
| `.` | one generation while stopped; `step 10` at the prompt for ten |
| `f7` | turn cell f7 on, or off again. Columns are `a` to `z`, rows 1 to 16 |
| `+`, `-` | wind the clock faster or slower; `speed 7` goes straight to a notch |
| `clear` | sweep the board |
| `why f7` | what the rule will do with that cell next, and why |

The board says when it has settled, is beating between two states, or is empty. None of that ends the game. Draw on it again and run.

## Snake

The arcade game for one to four. The snakes move on their own and you only steer. Start with `dotnet run -- snake play 1`, or `play 2` for two snakes at one keyboard.

**Moving.** Each snake starts three segments long and moves one square a beat, the way its head faces. Eating the food adds a segment, and the next piece lands at once. A snake stops when its head meets the wall, another snake, or itself, and what is left of it lies where it fell.

**Winning.** Alone, the game ends when your snake stops, and your score is what it ate. With more, the last snake still moving wins.

| Type or press | What happens |
| --- | --- |
| the arrows | turn Snake A; `wasd` turns Snake B |
| `north`, `east`, `south`, `west` | turn Snake A that way at the prompt; `b north` turns Snake B |
| `+`, `-` | wind the clock faster or slower; `speed 7` goes straight to a notch |
| `why north` | what is one square that way, before you commit |

A snake cannot turn back into its own neck. The clock starts at notch 5 of 9 and tightens on its own as the best-fed snake eats. Settings can also deal Snake a step at a time, where each snake moves one square per turn in seat order.

## Cascade

A puzzle for one. The board is 256 elbows, 16 by 16, each a cell with two arms. You have twelve touches. Touch a cell, watch the turning spread, and score for the rows, columns and squares that turn. Start with `dotnet run -- cascade play 1`.

**A touch** sets a cell turning. On each beat of the clock, every turning cell lands a quarter turn to the right. Each landed cell then reaches out along its two arms, and every neighbour with an arm pointing back begins turning. The cascade is at rest when a wave sets nothing off, and only then is the next touch allowed.

**Scoring.** Each cascade counts its turns, every row or column all sixteen of whose cells turned, and every two-by-two square all four of whose cells did. The totals add up over twelve cascades. The board is over when the twelfth comes to rest.

| Type or press | What happens |
| --- | --- |
| `f7` | touch that cell. Columns are `a` to `p`, rows 1 to 16 |
| `w`, `a`, `s`, `d` | move the hand one cell; `press` touches the cell under it |
| `why f7` | what that cell would reach when it lands, and whether anything reaches back |
| `+`, `-` | wind the clock a notch; `speed 7` goes straight to one |
| `resign` | put the board down with touches unspent |

The hand starts in the middle, on `h8`. A touch is refused while anything is still turning.

## Warband

Two squads of five, mustered out of each other's sight, then a battle nobody plays. All your decisions are made before the first blow. Start with `dotnet run -- warband play 2`.

**The muster.** Each squad has ten hexes in three ranks: `f1` to `f3` at the front, `m1` to `m4` in the middle, `b1` to `b3` at the back. Squads take turns placing one unit at a time until each has five, at most two of a kind. Neither sees the other's until both are on the field.

| Type | What happens |
| --- | --- |
| `bowman b2` | a bowman on hex b2; the kinds are `foot`, `pike`, `bow`, `ride`, `mend` and `ward` |
| `engage 3` | stand the lines 3 hexes apart, from 1 to 9, while the muster is on |
| `why bowman`, `why m2` | what a kind does from each rank, or what a hex touches |
| `p` | stop or start the battle; `.` steps one beat by hand |

**Where a unit stands is what it does.** A footman strikes twice from the front and once from the middle. A spearman strikes hardest from the front and reaches two hexes. A bowman shoots from the middle or back and finds whoever is nearest to falling. A rider charges three times from the front and does nothing at the back. A mender heals the most-hurt neighbour. A warder steps in front of any blow aimed at a unit it touches.

**The battle** runs on its own once the tenth unit is placed. Every unit acts once a round, quickest first. Strikes fall on the foremost rank still standing. Ground wider than a unit's reach leaves it out, so `engage` decides who can act at all. The squad with anybody left standing wins.
