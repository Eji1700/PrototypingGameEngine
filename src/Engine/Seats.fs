namespace TCModel.Engine

type PlayerId = private PlayerId of int

module Seat =

    let at place = PlayerId place

module PlayerId =
    let value (PlayerId n) = n
