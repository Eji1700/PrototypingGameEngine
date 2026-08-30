# One game, published, serving a house. The entry point is the game and the command is its
# configuration: `docker run <image> house --code hunter2` puts a word at the door, and anything
# the game takes on a command line it takes here the same way. Run with no command at all it opens
# a house on PORT, taking up the records under /data/logs on the way.
ARG GAME=Turncoats
ARG RUNTIME=linux-x64
ARG PORT=5000


FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG GAME
ARG RUNTIME
WORKDIR /src

# The project files alone first, and the restore on them, so that editing a source does not fetch
# every package again: a layer is kept for as long as what went into it is unchanged.
COPY Directory.Build.props ./
COPY src/Prototyping.Engine.fsproj src/
COPY src/Table/Prototyping.Table.fsproj src/Table/
COPY src/Net/Prototyping.Net.fsproj src/Net/
COPY src/Play/Prototyping.Play.fsproj src/Play/
COPY src/Games/${GAME}/${GAME}.fsproj src/Games/${GAME}/

RUN dotnet restore "src/Games/${GAME}/${GAME}.fsproj" -r ${RUNTIME}

COPY . .

RUN dotnet publish "src/Games/${GAME}/${GAME}.fsproj" \
        --no-restore \
        -c Release -r ${RUNTIME} \
        -p:PublishSingleFile=true \
        -p:PublishTrimmed=false \
        -o /out


FROM mcr.microsoft.com/dotnet/aspnet:10.0
ARG GAME
ARG PORT

# curl is here for the health check and for nothing else.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# The records go under /data/logs, a volume, so that they outlive the container: with --fill a
# restart takes them back up, and is a pause rather than a loss.
WORKDIR /data
RUN mkdir -p /data/logs && chown -R app:app /data
VOLUME /data/logs

COPY --from=build --chown=app:app /out /app

USER app

ENV GAME=${GAME} PORT=${PORT}
EXPOSE ${PORT}

# Answering at all is the health: a house with a word at the door answers a stranger with the
# door, and is up.
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s \
    CMD curl -s -o /dev/null "http://localhost:${PORT}/" || exit 1

# The door is kept unless the command says --open: a house mints a word when none is given and
# says it in `docker logs`, so a container run bare is a room only whoever reads the log can enter.
# The default command lives here rather than in a CMD so that it can read PORT, which a CMD in
# exec form cannot; a command given to `docker run` replaces it whole.
ENTRYPOINT ["/bin/sh", "-c", "[ $# -eq 0 ] && set -- house --port \"$PORT\" --fill; exec \"/app/$GAME\" \"$@\"", "--"]
