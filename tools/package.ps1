param(
    [string]$Name = "Consumer",
    [int]$Port = 5300
)

# Whether the engine works as a *dependency*, which is not what the eight games here prove. They
# reach it by project reference, side by side in one solution and one restore; somebody building a
# game of their own gets four packages off a feed. That is a different question - the version graph
# has to line up, the framework reference has to travel, and the browser's client has to come out
# of a packed assembly rather than off disk - and this is the only thing that asks it.
#
# So: pack the four, generate a game from the template into a directory outside the repository,
# point it at the packages and nothing else, and see whether it plays.

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$failed = 0

. (Join-Path $PSScriptRoot "Driving.ps1")

$version = ([xml](Get-Content (Join-Path $root "Directory.Build.props"))).Project.PropertyGroup.PrototypingVersion
$feed = Join-Path $root "publish/packages"
$template = Join-Path $root "templates/game"

# Outside the repository on purpose: a consumer that happened to sit inside it could reach the
# projects by accident, and then this would be proving nothing.
$outside = Join-Path ([IO.Path]::GetTempPath()) "proto-consumer-$PID"
$into = Join-Path $outside $Name

# The consumer writes its record into whatever directory it is run from, and logs/ here is
# committed on purpose - so what is already there is noted, and anything new is taken away after.
$logs = Join-Path $root "logs"
$before = @(Get-ChildItem $logs -Filter *.log -ErrorAction SilentlyContinue | ForEach-Object { $_.Name })

$installed = $false

try {
    "Packing $version..."
    Remove-Item $feed -Recurse -Force -ErrorAction SilentlyContinue

    foreach ($project in @(
            "src/Prototyping.Engine.fsproj"
            "src/Table/Prototyping.Table.fsproj"
            "src/Net/Prototyping.Net.fsproj"
            "src/Play/Prototyping.Play.fsproj")) {

        dotnet pack (Join-Path $root $project) -c Release -o $feed | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "$project would not pack" }
    }

    $packed = @(Get-ChildItem $feed -Filter *.nupkg | ForEach-Object { $_.Name })
    Report "the four projects pack" ($packed.Count -eq 4) "$($packed.Count) packages came out: $($packed -join ', ')"

    dotnet new install $template --force | Out-Null
    $installed = $true

    New-Item -ItemType Directory -Force -Path $outside | Out-Null
    dotnet new proto-game -n $Name -o $into | Out-Null

    # The packed feed and nuget.org, and nothing else: the four packages come from the folder just
    # packed, and what they depend on - FSharp.Core, Falco, Argu - from the one place those live. Any
    # other feed on this machine is shut out, so a package that happened to be cached elsewhere
    # could not stand in for the one that was meant to be tested.
    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="packed" value="$feed" />
    <add key="nuget" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
"@ | Set-Content (Join-Path $into "nuget.config") -Encoding utf8

    # The one line that makes this a consumer rather than another project in the solution.
    $project = Join-Path $into "$Name.fsproj"
    (Get-Content $project -Raw) -replace `
        '<ProjectReference Include="[^"]*Prototyping.Play.fsproj" />', `
        "<PackageReference Include=`"Prototyping.Play`" Version=`"$version`" />" |
        Set-Content $project -Encoding utf8

    Report "and a game outside the repository asks for them by name" (
        (Get-Content $project -Raw) -notmatch "ProjectReference") "the project reference is still there"

    # Restored into a folder of its own rather than the machine's package cache: the version has not
    # changed since the last time this ran, and NuGet would hand back the 0.5.0 it cached then - an
    # engine packed an hour ago standing in for the one packed a minute ago.
    $packages = Join-Path $outside "packages"
    $built = dotnet build $project -c Release -p:RestorePackagesPath=$packages | Out-String
    Report "it restores and builds against the packages alone" ($LASTEXITCODE -eq 0) (
        ($built -split "`n" | Select-String "error" | Select-Object -First 1))

    if ($LASTEXITCODE -eq 0) {
        $framework = ([xml](Get-Content $project)).Project.PropertyGroup.TargetFramework
        $exe = Join-Path $into "bin/Release/$framework/$Name"
        if (Test-Path "$exe.exe") { $exe = "$exe.exe" }

        $said = (@("2", "quit") | & $exe play 2) | Out-String
        $played = $said -replace "$([char]27)\[[0-9;]*m", ""
        Report "and plays" ($played -match "takes 2 tokens") "nothing was taken"

        # The one thing a package can break that a project reference never would: datastar.js is an
        # embedded resource read out of Prototyping.Net's own assembly, and this is where a packed
        # assembly that lost it would say so.
        $here = Split-Path -Parent $exe
        $served = Start-Console $exe "serve 2 --port $Port --open" $here

        try {
            Wait-ForPort $Port 60
            $page = (Invoke-WebRequest -Uri "http://localhost:$Port/" -UseBasicParsing -TimeoutSec 20).Content
            Report "and serves a page, so the whole stack came off the feed" ($page -match "id=`"screen`"") "no board slot on the page"
            Report "with the browser's client out of the packed assembly" (
                (Invoke-WebRequest -Uri "http://localhost:$Port/datastar.js" -UseBasicParsing -TimeoutSec 20).StatusCode -eq 200) "the client was not served"
        }
        finally {
            Close-Console $served
            Stop-Tables
        }
    }
}
finally {
    Remove-Item $outside -Recurse -Force -ErrorAction SilentlyContinue

    Get-ChildItem $logs -Filter *.log -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -notin $before } |
        Remove-Item -Force

    if ($installed) { dotnet new uninstall $template 2>&1 | Out-Null }
}

Finish "check"
