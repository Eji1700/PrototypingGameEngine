namespace TCModel.MyGame

/// The row the game is played on. Two numbers, and every rule below this file is about them.
module Row =

    [<Literal>]
    let Dealt = 15

    [<Literal>]
    let Most = 3

    let takeable count = count >= 1 && count <= Most
