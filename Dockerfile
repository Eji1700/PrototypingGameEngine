ARG GAME=Turncoats
ARG RUNTIME=linux-x64


FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG GAME
ARG RUNTIME
WORKDIR /src

COPY . .

RUN dotnet publish "src/Games/${GAME}/${GAME}.fsproj" \
        -c Release -r ${RUNTIME} \
        -p:SelfContained=false \
        -p:PublishSingleFile=true \
        -p:PublishTrimmed=false \
        -o /out


FROM mcr.microsoft.com/dotnet/aspnet:10.0
ARG GAME

WORKDIR /data
RUN mkdir -p /data/logs && chown -R app:app /data
VOLUME /data/logs

COPY --from=build --chown=app:app /out /app

USER app

EXPOSE 5000

ENV GAME=${GAME}
ENTRYPOINT ["/bin/sh", "-c", "exec \"/app/$GAME\" \"$@\"", "--"]
CMD ["house", "--port", "5000", "--open"]
