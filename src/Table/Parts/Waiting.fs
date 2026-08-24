namespace Prototyping.Table

open Prototyping.Engine

type Waiting =
    { Player: PlayerId
      Expected: bool
      Away: bool
      Yours: bool }
