module TCModel.Diplomacy.Program

/// This game, on its own. The same door as every other game's, for the reason
/// [Turncoats' one](../Turncoats/Program.fs) gives.
[<EntryPoint>]
let main argv = TCModel.Play.only Offer.ways argv
