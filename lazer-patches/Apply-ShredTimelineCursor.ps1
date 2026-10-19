param(
	[string]$LazerRoot = 'C:\Users\Utilisateur\source\osu-shred-lazer'
)

$ErrorActionPreference = 'Stop'
$editorPlayer = Join-Path $LazerRoot 'osu.Game\Screens\Edit\GameplayTest\EditorPlayer.cs'
if (-not (Test-Path -LiteralPath $editorPlayer -PathType Leaf)) {
	throw "EditorPlayer.cs introuvable : $editorPlayer"
}

$encoding = New-Object System.Text.UTF8Encoding($false)
$source = [System.IO.File]::ReadAllText($editorPlayer, $encoding)
$marker = '// Shred: start editor test at the audio timeline cursor.'
$pattern = '(?m)^(?<indent>[ \t]*)if[ \t]*\(\s*editorState\.Time\s*>\s*gameplayStart(?:\s*&&\s*editorState\.Time\s*>\s*DrawableRuleset\.Objects\.FirstOrDefault\(\)\?\.StartTime)?\s*\)\s*(?:(?<block>\{)\s*)?(?<reset>[A-Za-z_][A-Za-z0-9_]*\.Reset\(editorState\.Time\);)(?(block)\s*\})'
$matchesFound = [System.Text.RegularExpressions.Regex]::Matches($source, $pattern)
if ($source.Contains($marker)) {
	if ($matchesFound.Count -ne 0) { throw 'Code de curseur ambigu. Aucun changement.' }
	Write-Host 'Le demarrage au curseur est deja applique.'
	return
}
if ($matchesFound.Count -ne 1) {
	throw "Bloc de demarrage attendu absent ou ambigu ($($matchesFound.Count) occurrences). Aucun changement. Joindre EditorPlayer.cs pour verification."
}

$newline = "`n"
if ($source.Contains("`r`n")) { $newline = "`r`n" }
$match = $matchesFound[0]
$indent = $match.Groups['indent'].Value
$replacement = $indent + $marker + $newline + $indent + $match.Groups['reset'].Value
$updated = $source.Substring(0, $match.Index) + $replacement + $source.Substring($match.Index + $match.Length)

$backup = "$editorPlayer.before-shred-timeline-cursor.bak"
if (-not (Test-Path -LiteralPath $backup)) {
	[System.IO.File]::WriteAllText($backup, $source, $encoding)
}
[System.IO.File]::WriteAllText($editorPlayer, $updated, $encoding)
Write-Host "Demarrage au curseur audio applique dans $editorPlayer"
Write-Host 'Recompiler ensuite osu.Desktop depuis osu-shred-lazer.'
