param (
    [string]$filePath
)

if (!(Test-Path $filePath)) {
    Write-Host "File not found: $filePath"
    exit 1
}

# List of keywords to remove
$keywords = @(
    '\bprivate\s?',     # Removes 'private' + optional space
    #'\bprotected\s?',   # Removes 'protected' + optional space
    '\breadonly\s?'     # Removes 'readonly' + optional space
)

# Skips a "..." or '...' literal starting at $i and returns the index after it
function Skip-Quoted([string]$s, [int]$i) {
    $q = $s[$i]
    $i++
    while ($i -lt $s.Length) {
        $c = $s[$i]
        if ($c -eq '\') { $i += 2; continue }
        if ($c -eq $q) { return $i + 1 }
        $i++
    }
    return $i
}

# Skips a string that starts at $i (the first of $, @ or ") and returns the index after it
function Skip-String([string]$s, [int]$i) {
    $interp = $false
    $verbatim = $false
    while ($s[$i] -ne '"') {
        if ($s[$i] -eq '$') { $interp = $true } else { $verbatim = $true }
        $i++
    }
    $i++
    while ($i -lt $s.Length) {
        $c = $s[$i]
        if ($c -eq '"') {
            if ($verbatim -and $s[$i + 1] -eq '"') { $i += 2; continue }
            return $i + 1
        }
        if ($c -eq '\' -and -not $verbatim) { $i += 2; continue }
        if ($interp -and $c -eq '{') {
            if ($s[$i + 1] -eq '{') { $i += 2; continue }
            $i = Skip-Hole $s ($i + 1)
            continue
        }
        if ($interp -and $c -eq '}' -and $s[$i + 1] -eq '}') { $i += 2; continue }
        $i++
    }
    return $i
}

# Skips an interpolation hole (code up to the matching closing brace) and returns the index after it
function Skip-Hole([string]$s, [int]$i) {
    $depth = 0
    while ($i -lt $s.Length) {
        $c = $s[$i]
        if ($c -eq '"' -or $c -eq '$' -or $c -eq '@') {
            if ($c -eq '"' -or $s[$i + 1] -eq '"' -or $s[$i + 1] -eq '$' -or $s[$i + 1] -eq '@') { $i = Skip-String $s $i; continue }
        }
        if ($c -eq "'") { $i = Skip-Quoted $s $i; continue }
        if ($c -eq '{') { $depth++ }
        elseif ($c -eq '}') {
            if ($depth -eq 0) { return $i + 1 }
            $depth--
        }
        $i++
    }
    return $i
}

# Removes 'public' from top-level type declarations only (they default to internal, and sibling types
# still see them). Nested types keep 'public' because their containing type exposes them to other types.
# The packaged script is the body of Program (depth 1) followed by a '}' that closes it, so the
# top-level types sit at depth 0.
function Remove-PublicFromTypes([string]$s) {
    $typeDecl = [regex]'\Gpublic\s?(?=(?:(?:abstract|static|sealed|partial)\s+)*(?:class|struct|enum|interface)\b)'
    $result = New-Object System.Text.StringBuilder
    $depth = 1
    $copyFrom = 0
    $i = 0
    while ($i -lt $s.Length) {
        $c = $s[$i]
        if ($c -eq '"' -or $c -eq '$' -or $c -eq '@') {
            if ($c -eq '"' -or $s[$i + 1] -eq '"' -or $s[$i + 1] -eq '$' -or $s[$i + 1] -eq '@') { $i = Skip-String $s $i; continue }
        }
        elseif ($c -eq "'") { $i = Skip-Quoted $s $i; continue }
        elseif ($c -eq '/' -and $s[$i + 1] -eq '/') {
            $end = $s.IndexOf("`n", $i)
            if ($end -lt 0) { break }
            $i = $end
            continue
        }
        elseif ($c -eq '{') { $depth++ }
        elseif ($c -eq '}') { $depth-- }
        elseif ($c -eq 'p' -and $depth -eq 0 -and ($i -eq 0 -or -not ([char]::IsLetterOrDigit($s[$i - 1]) -or $s[$i - 1] -eq '_'))) {
            $m = $typeDecl.Match($s, $i)
            if ($m.Success) {
                [void]$result.Append($s, $copyFrom, $i - $copyFrom)
                $i += $m.Length
                $copyFrom = $i
                continue
            }
        }
        $i++
    }
    [void]$result.Append($s, $copyFrom, $s.Length - $copyFrom)
    return $result.ToString()
}

# Read file content
$content = Get-Content $filePath -Raw

# Apply all keyword removals
foreach ($keyword in $keywords) {
    $content = $content -replace $keyword, ''
}

$content = Remove-PublicFromTypes $content

# Save back to the file
Set-Content -Path $filePath -Value $content

Write-Host "Minification complete: $filePath"
