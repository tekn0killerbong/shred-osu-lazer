$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    dotnet build .\Shred.Editor.Tests\Shred.Editor.Tests.csproj -m:1
    if ($LASTEXITCODE -ne 0) { throw 'Compilation des tests éditeur échouée.' }
    dotnet run --no-build --project .\Shred.Editor.Tests\Shred.Editor.Tests.csproj
    if ($LASTEXITCODE -ne 0) { throw 'Tests éditeur/gameplay échoués.' }
    dotnet run --project .\Shred.StepRecorder.Tests\Shred.StepRecorder.Tests.csproj
    if ($LASTEXITCODE -ne 0) { throw 'Tests Step recorder échoués.' }
} finally {
    Pop-Location
}
