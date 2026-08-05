namespace TCModel.Console

open TCModel.Domain

/// A seat at a table that has not filled up yet, as the person waiting sees it.
///
/// This is the one screen a player reads that is not drawn from a game, because there is
/// no game to draw yet - only a list of who has arrived. It is described here, in the layer
/// that does the showing, so that a view can lay it out like everything else and nothing
/// above this layer has to know how a screen is put together.
type Waiting =
    {
        Player: PlayerId
        /// Nobody has taken this seat yet.
        Expected: bool
        /// Taken, but whoever took it has lost their console.
        Away: bool
        /// The seat belonging to whoever is reading.
        Yours: bool
    }
