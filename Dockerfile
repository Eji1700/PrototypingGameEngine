# One game, one port, one image - which is what splitting the games into their own
# executables was for.
#
#   docker build -t turncoats .
#   docker build -t tictactoe --build-arg GAME=TicTacToe .
#   docker run -p 5000:5000 -v tcmodel-logs:/data/logs turncoats
#
# The game is a build argument rather than four Dockerfiles, because the four differ in one
# word and nothing else. `GAME` is the project's own name, which is also its assembly's name
# and the name of the file that comes out - the three being one thing is what `Invoked` leans
# on to work out what to call itself, so it is one thing here too.

ARG GAME=Turncoats
ARG RUNTIME=linux-x64

# --- building ---------------------------------------------------------------------------

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG GAME
ARG RUNTIME
WORKDIR /src

# The whole tree, because a project reference is a graph and restoring one game means
# restoring the engine under it. `.dockerignore` is what keeps this honest - without it this
# line copies `bin`, `obj`, every published binary and the git history into the build.
COPY . .

# Framework-dependent, and not trimmed. Both of those are load-bearing and both are explained
# at length in `tools/publish.ps1`: `Launch` builds its command line by reflecting over the
# argument types, SignalR finds a hub's methods by name, and `Page.Signals` is read off a
# request by a serialiser that reflects - so a trimmed build emits no warning whatsoever and
# throws on the first line it is given.
#
# `-p:SelfContained=false` rather than `--self-contained false`, which is the other thing that
# file has learned: the flag sets the property on the project named and on nothing else, and a
# `-p:` on the command line is a global property that reaches every project in the graph.
RUN dotnet publish "src/Games/${GAME}/${GAME}.fsproj" \
        -c Release -r ${RUNTIME} \
        -p:SelfContained=false \
        -p:PublishSingleFile=true \
        -p:PublishTrimmed=false \
        -o /out

# --- running ----------------------------------------------------------------------------

FROM mcr.microsoft.com/dotnet/aspnet:10.0
ARG GAME

# Where records are written, and the one directory worth keeping. `Transcript` files a game
# under `logs/` beside wherever the game was started from, so the working directory is the
# whole of the arrangement - and a house takes its games back up from exactly these files, so
# a volume here is the difference between a restart being a pause and being a loss.
WORKDIR /data
RUN mkdir -p /data/logs && chown -R app:app /data
VOLUME /data/logs

COPY --from=build --chown=app:app /out /app

# Not root. A table anybody can reach is a table anybody can send anything to, and the
# process that reads it should be able to do as little as possible with what it is sent. The
# image ships with this user already made.
USER app

EXPOSE 5000

# The command line is the configuration, and there is deliberately no second way to say any
# of it. This program has one language for what to open and how far it reaches - the same one
# a person types, the same one it writes back out - and a set of environment variables would
# be a second, to be kept in step with the first for ever. So the entry point is the game and
# the command is a default anybody may replace:
#
#   docker run turncoats house --code hunter2
#   docker run turncoats house --behind --at stones.example.org
#   docker run turncoats host 3 --open          # one table rather than a house of them
#
# `--open` here because an image nobody has configured should not invent a word at the door
# that only the container's log has seen. A house on anything but a private network wants
# `--code` given, and its own README says so.
# The name has to reach the entry point through the environment and a shell, and that is not
# decoration. An `ENTRYPOINT` in exec form does no substitution at all - `["/app/${GAME}"]`
# looks for a file with a dollar sign in its name and fails at `docker run`, not at build,
# which is the worst moment to find out. Shell form would fix the substitution and break
# signals: the game would run as a child of `sh`, and `docker stop` would reach the shell
# rather than the table.
#
# So: `exec` replaces the shell with the game, keeping it as PID 1 where a stop signal can
# find it, and `"$@"` hands on whatever `CMD` or the command line said. The binary keeps its
# own name through all of it, which matters more here than it looks - `Invoked` works out what
# to tell players to type by reading the running file's name, so a game copied to `/app/game`
# would print instructions naming a program nobody has.
ENV GAME=${GAME}
ENTRYPOINT ["/bin/sh", "-c", "exec \"/app/$GAME\" \"$@\"", "--"]
CMD ["house", "--port", "5000", "--open"]
