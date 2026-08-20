namespace TCModel.Table

open TCModel.Engine

type Waiting =
    { Player: PlayerId
      Expected: bool
      Away: bool
      Yours: bool }
