namespace Prototyping.Engine

[<NoComparison; NoEquality>]
type Rules<'Move, 'State, 'Notice> =
    { Deal: int -> uint64 -> Result<'State, string>

      Play: 'Move -> 'State -> 'State option * 'Notice list

      Active: 'State -> PlayerId

      Turn: 'State -> int

      Over: 'State -> bool

      Seats: 'State -> int

      Reseed: 'State -> uint64 }
