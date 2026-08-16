module TCModel.Turncoats.Program

/// This game, on its own.
///
/// Every game's door is this file and this file is the same at all of them, which is what
/// having two seams was for: by the time a game is a `Playable` there is nothing left for a
/// way in to decide. What a line means, what the menu offers, where a record goes and how far
/// a table reaches are all settled above, generically, and none of it is written twice.
///
/// Spelt out in full because this game has a `Play` of its own - what a turn of it is - and a
/// game's own names win inside its own namespace. Which is the right way round: the engine is
/// the thing being reached out to here, and reaching out to it should say so.
[<EntryPoint>]
let main argv = TCModel.Play.only Offer.ways argv
