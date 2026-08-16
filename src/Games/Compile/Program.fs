module TCModel.Compile.Program

/// This game, on its own. The same door as every other game's, for the reason
/// [Turncoats' one](../Turncoats/Program.fs) gives.
///
/// The optional rule is not a second game and is not opened here as one. Which variant is in
/// play is a thing about *this* game that a player settles, so it is settled where a player
/// settles things - on the game's own page of the settings screen.
[<EntryPoint>]
let main argv = TCModel.Play.only Offer.ways argv
