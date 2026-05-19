param(
    [string]$RootPath = ".",
    [string[]]$Extensions = @('*.cs'),
    [string[]]$ExcludeDirs = @('bin','obj','.git')
)

function Should-ExcludePath($path) {
    foreach ($ex in $ExcludeDirs) {
        if ($path -like "*\\$ex\\*" -or $path -like "*\\$ex") { return $true }
    }
    return $false
}

function Strip-Comments($text) {
    $sb = New-Object System.Text.StringBuilder
    $len = $text.Length
    $i = 0
    $inString = $false
    $inChar = $false
    $inLineComment = $false
    $inBlockComment = $false

    while ($i -lt $len) {
        $c = $text[$i]
        $next = if ($i + 1 -lt $len) { $text[$i+1] } else { [char]0 }

        if ($inLineComment) {
            if ($c -eq "`n" -or $c -eq "`r") {
                $inLineComment = $false
                $sb.Append($c) | Out-Null
            }
            $i++
            continue
        }

        if ($inBlockComment) {
            if ($c -eq '*' -and $next -eq '/') {
                $inBlockComment = $false
                $i += 2
                continue
            }
            $i++
            continue
        }

        if (-not $inString -and -not $inChar) {
            if ($c -eq '/' -and $next -eq '/') {
                # Check for XML doc comment '///' - preserve whole line
                $third = if ($i + 2 -lt $len) { $text[$i+2] } else { [char]0 }
                if ($third -eq '/') {
                    # append the rest of the line as-is
                    $sb.Append('///') | Out-Null
                    $i += 3
                    while ($i -lt $len) {
                        $ch = $text[$i]
                        $sb.Append($ch) | Out-Null
                        $i++
                        if ($ch -eq "`n") { break }
                    }
                    continue
                } else {
                    # start of normal line comment -> skip until EOL
                    $inLineComment = $true
                    $i += 2
                    continue
                }
            }

            if ($c -eq '/' -and $next -eq '*') {
                # start block comment
                $inBlockComment = $true
                $i += 2
                continue
            }

            if ($c -eq '"') { $inString = $true; $sb.Append($c) | Out-Null; $i++; continue }
            if ($c -eq "'") { $inChar = $true; $sb.Append($c) | Out-Null; $i++; continue }
        } else {
            # inside string or char literal
            if ($c -eq '\\') {
                # escaped char, copy escape and next char
                $sb.Append($c) | Out-Null
                $i++
                if ($i -lt $len) { $sb.Append($text[$i]) | Out-Null; $i++ }
                continue
            }
            if ($inString -and $c -eq '"') { $inString = $false; $sb.Append($c) | Out-Null; $i++; continue }
            if ($inChar -and $c -eq "'") { $inChar = $false; $sb.Append($c) | Out-Null; $i++; continue }
            $sb.Append($c) | Out-Null
            $i++
            continue
        }

        # default: copy char
        $sb.Append($c) | Out-Null
        $i++
    }

    return $sb.ToString()
}

# Find files
$files = Get-ChildItem -Path $RootPath -Recurse -Include $Extensions -File | Where-Object { -not (Should-ExcludePath($_.FullName)) }

foreach ($file in $files) {
    Write-Host "Processing: $($file.FullName)"
    $original = Get-Content -Raw -LiteralPath $file.FullName -ErrorAction Stop
    $stripped = Strip-Comments $original
    if ($stripped -ne $original) {
        Set-Content -LiteralPath $file.FullName -Value $stripped -Encoding UTF8
        Write-Host "Updated: $($file.FullName)"
    } else {
        Write-Host "No changes: $($file.FullName)"
    }
}

Write-Host "Done processing files."
