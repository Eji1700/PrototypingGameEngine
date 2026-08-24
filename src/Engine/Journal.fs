namespace Prototyping.Engine

type Entry<'Move, 'Notice> =
    { Ordinal: int
      Turn: int
      Actor: PlayerId
      Asked: Msg<'Move>
      Told: Told<'Move, 'Notice> list }

type Journal<'Move, 'Notice> =
    private
        { Players: int
          Seed: uint64
          // Newest first, since writing is the common case; `entries` puts them back into
          // the order they were played.
          Written: Entry<'Move, 'Notice> list }

module Journal =

    let ofDeal players seed =
        { Players = players
          Seed = seed
          Written = [] }

    let players journal = journal.Players

    let seed journal = journal.Seed

    let length journal = List.length journal.Written

    let isEmpty journal = List.isEmpty journal.Written

    let entries journal = List.rev journal.Written

    let write turn actor asked told journal =
        { journal with
            Written =
                { Ordinal = length journal + 1
                  Turn = turn
                  Actor = actor
                  Asked = asked
                  Told = told }
                :: journal.Written }

    let moves journal =
        entries journal |> List.map (fun entry -> entry.Asked)
