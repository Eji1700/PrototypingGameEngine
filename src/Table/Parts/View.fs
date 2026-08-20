namespace TCModel.Table

open TCModel.Engine

type Shown =
    | AtATerminal
    | InABrowser

[<NoComparison; NoEquality>]
type View<'Move, 'State, 'Notice> =
    { Name: string
      Describe: string

      Shown: Shown

      Palette: Palette

      Board: Margins -> PlayerId -> Model<'Move, 'State, 'Notice> -> string

      History: PlayerId -> Model<'Move, 'State, 'Notice> -> string

      Answer: PlayerId -> string -> Model<'Move, 'State, 'Notice> -> string

      Rules: string

      Says: string -> string

      Waiting: Waiting list -> string }
