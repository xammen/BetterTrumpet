# BetterTrumpet Release Script
# Automates the build, packaging, and release process for x86, x64, and arm64.

param(
    [switch]$SkipBuild,
    [switch]$SkipGit,
    [switch]$SkipGitHub,
    [switch]$SkipChocolatey,
    [string]$InnoSetup
)

$ErrorActionPreference = "Stop"
$Version = "3.4.0"
$Architectures = @('x86', 'x64', 'arm64')

# Map architecture -> build output dir, installer suffix, and portable suffix.
$ArchMap = @{
    x86   = @{ BuildDir = 'Build\Release';       Suffix = '' }
    x64   = @{ BuildDir = 'Build\Release-x64';   Suffix = '-x64' }
    arm64 = @{ BuildDir = 'Build\Release-arm64'; Suffix = '-arm64' }
}

$ReleaseNotesFile = ".claude\release-$Version-notes.md"

Write-Host "🚀 BetterTrumpet $Version Release Process" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

# Preflight: step 6 needs the notes file, and failing now is much cheaper than failing
# after step 5 has already pushed the tag.
if (-not $SkipGitHub -and -not (Test-Path $ReleaseNotesFile)) {
    Write-Host "❌ Release notes not found: $ReleaseNotesFile" -ForegroundColor Red
    exit 1
}

# ============================================================================
# STEP 1: Build Release (all architectures)
# ============================================================================
if (-not $SkipBuild) {
    Write-Host "📦 Step 1: Building Release..." -ForegroundColor Yellow

    foreach ($arch in $Architectures) {
        $buildDir = $ArchMap[$arch].BuildDir
        Write-Host "  Building Release $arch..."

        # Drop the previous run's binary first, so the existence check below cannot be
        # satisfied by a stale exe if the build fails to produce a new one.
        Remove-Item "$buildDir\BetterTrumpet.exe" -Force -ErrorAction SilentlyContinue

        # Build the app project rather than the solution. EarTrumpet.ColorTool is a legacy
        # x86-only dev utility whose csproj defines OutputPath only for Debug|x86 and
        # Release|x86, so a solution build fails outright on x64/arm64 with "BaseOutputPath/
        # OutputPath property is not set"; EarTrumpet.Package is x86-only Store packaging
        # (AppxBundlePlatforms=x86). Neither ships in the installer or the portable ZIP.
        & dotnet build EarTrumpet\EarTrumpet.csproj --no-incremental -c Release -p:Platform=$arch -v:minimal --nologo

        # $ErrorActionPreference does not apply to native command exit codes, so check
        # explicitly. Both conditions are independently fatal.
        if ($LASTEXITCODE -ne 0) {
            Write-Host "❌ Build failed for $arch (dotnet build exit code $LASTEXITCODE)!" -ForegroundColor Red
            exit 1
        }
        if (-not (Test-Path "$buildDir\BetterTrumpet.exe")) {
            Write-Host "❌ Build for $arch produced no $buildDir\BetterTrumpet.exe!" -ForegroundColor Red
            exit 1
        }
    }

    Write-Host "  ✅ Build successful!" -ForegroundColor Green
    Write-Host ""
} else {
    Write-Host "⏭️  Skipping build (using existing)" -ForegroundColor Gray
    Write-Host ""
}

# ============================================================================
# STEP 2: Create Installers with Inno Setup (all architectures)
# ============================================================================
Write-Host "📦 Step 2: Creating Installers..." -ForegroundColor Yellow

# Inno Setup's install location varies by major version and by per-user vs machine-wide
# install (6.x defaults under "Program Files (x86)", 7.x under %LOCALAPPDATA%\Programs), so
# probe the known layouts and fall back to PATH rather than pinning one absolute path.
if (-not $InnoSetup) {
    $innoCandidates = @()
    foreach ($root in @((Join-Path $env:LOCALAPPDATA 'Programs'), ${env:ProgramFiles(x86)}, $env:ProgramFiles)) {
        if ($root) {
            foreach ($major in 7, 6) {
                $innoCandidates += (Join-Path $root "Inno Setup $major\ISCC.exe")
            }
        }
    }
    $InnoSetup = $innoCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1

    if (-not $InnoSetup) {
        $onPath = Get-Command ISCC.exe -ErrorAction SilentlyContinue
        if ($onPath) { $InnoSetup = $onPath.Source }
    }
    if (-not $InnoSetup) {
        Write-Host "❌ Inno Setup compiler (ISCC.exe) not found. Looked in:" -ForegroundColor Red
        $innoCandidates | ForEach-Object { Write-Host "     $_" -ForegroundColor Red }
        Write-Host "   Install Inno Setup 6 or 7, put ISCC.exe on PATH, or pass -InnoSetup <path>." -ForegroundColor Red
        exit 1
    }
} elseif (-not (Test-Path $InnoSetup)) {
    Write-Host "❌ -InnoSetup path does not exist: $InnoSetup" -ForegroundColor Red
    exit 1
}
Write-Host "  Using $InnoSetup"

$Installers = @{}
foreach ($arch in $Architectures) {
    $suffix = $ArchMap[$arch].Suffix
    $InstallerPath = "dist\BetterTrumpet-$Version-setup$suffix.exe"

    # Same reasoning as the build step: clear the stale artifact before regenerating it.
    Remove-Item $InstallerPath -Force -ErrorAction SilentlyContinue

    Write-Host "  Running Inno Setup Compiler for $arch..."
    & $InnoSetup "/DArch=$arch" installer.iss

    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Inno Setup failed for $arch (exit code $LASTEXITCODE)!" -ForegroundColor Red
        exit 1
    }
    if (-not (Test-Path $InstallerPath)) {
        Write-Host "❌ Installer not created for $arch!" -ForegroundColor Red
        exit 1
    }

    $Installers[$arch] = $InstallerPath
    $InstallerSize = [math]::Round((Get-Item $InstallerPath).Length / 1MB, 2)
    Write-Host "  ✅ Installer created: $InstallerPath ($InstallerSize MB)"
}
Write-Host ""

# ============================================================================
# STEP 3: Create Portable Packages (all architectures)
# ============================================================================
Write-Host "📦 Step 3: Creating Portable Packages..." -ForegroundColor Yellow

$Portables = @{}
foreach ($arch in $Architectures) {
    $suffix = $ArchMap[$arch].Suffix
    $PortablePath = "dist\BetterTrumpet-$Version-portable$suffix.zip"

    Remove-Item $PortablePath -Force -ErrorAction SilentlyContinue

    Write-Host "  Packaging portable $arch..."
    try {
        & ".\build-portable.ps1" -Arch $arch
    } catch {
        Write-Host "❌ Portable packaging failed for ${arch}: $_" -ForegroundColor Red
        exit 1
    }

    if (-not (Test-Path $PortablePath)) {
        Write-Host "❌ Portable ZIP not created for $arch!" -ForegroundColor Red
        exit 1
    }

    $Portables[$arch] = $PortablePath
    $PortableSize = [math]::Round((Get-Item $PortablePath).Length / 1MB, 2)
    Write-Host "  ✅ Portable created: $PortablePath ($PortableSize MB)"
}
Write-Host ""

# ============================================================================
# STEP 4: Calculate Checksums
# ============================================================================
Write-Host "🔐 Step 4: Calculating Checksums..." -ForegroundColor Yellow

$Checksums = @{}
foreach ($arch in $Architectures) {
    $Checksums[$arch] = (Get-FileHash -Path $Installers[$arch] -Algorithm SHA256).Hash
    Write-Host "  $arch SHA256: $($Checksums[$arch])" -ForegroundColor Cyan
}

# Update Chocolatey checksums. The pattern matches whatever the current value is — a
# placeholder or a hash written by an earlier run — so re-running the release refreshes
# them instead of silently shipping the previous run's values.
Write-Host "  Updating chocolatey checksums..."
$chocoFile = "chocolatey\tools\chocolateyInstall.ps1"
$chocoVars = @{ x86 = 'checksumX86'; x64 = 'checksumX64'; arm64 = 'checksumArm64' }
$chocoInstall = Get-Content $chocoFile -Raw
foreach ($arch in $Architectures) {
    $pattern = '(?m)^(\s*\$' + $chocoVars[$arch] + '\s*=\s*'')[^'']*('')'
    if ($chocoInstall -notmatch $pattern) {
        Write-Host ("❌ No " + $chocoVars[$arch] + " assignment found in $chocoFile!") -ForegroundColor Red
        exit 1
    }
    $chocoInstall = $chocoInstall -replace $pattern, ('${1}' + $Checksums[$arch] + '${2}')
}
Set-Content $chocoFile $chocoInstall -NoNewline

# Update Winget checksums (per-architecture installer entries)
Write-Host "  Updating winget checksums..."
$wingetFiles = @(
    "winget-manifest\xmn.BetterTrumpet.installer.yaml",
    "winget-manifest\manifests\x\xmn\BetterTrumpet\$Version\xmn.BetterTrumpet.installer.yaml"
)
foreach ($wingetFile in $wingetFiles) {
    if (-not (Test-Path $wingetFile)) {
        Write-Host "❌ Winget manifest not found: $wingetFile" -ForegroundColor Red
        exit 1
    }

    $raw = Get-Content $wingetFile -Raw
    $newline = if ($raw.Contains("`r`n")) { "`r`n" } else { "`n" }
    $lines = $raw -split "`r?`n"

    # Rewrite each InstallerSha256 under the Architecture entry it belongs to, rather than
    # keying off placeholder tokens or assuming the entries appear in a fixed order.
    $currentArch = $null
    $seen = @{}
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^\s*-\s*Architecture:\s*(\S+)\s*$') {
            $currentArch = $Matches[1]
        }
        elseif ($lines[$i] -match '^(\s*InstallerSha256:\s*)\S+\s*$') {
            if (-not $currentArch) {
                Write-Host "❌ InstallerSha256 before any Architecture entry in $wingetFile!" -ForegroundColor Red
                exit 1
            }
            if (-not $Checksums.ContainsKey($currentArch)) {
                Write-Host "❌ No checksum computed for architecture '$currentArch' in $wingetFile!" -ForegroundColor Red
                exit 1
            }
            $lines[$i] = $Matches[1] + $Checksums[$currentArch]
            $seen[$currentArch] = $true
        }
    }

    foreach ($arch in $Architectures) {
        if (-not $seen[$arch]) {
            Write-Host "❌ No InstallerSha256 entry for $arch in $wingetFile!" -ForegroundColor Red
            exit 1
        }
    }

    Set-Content $wingetFile ($lines -join $newline) -NoNewline
}

Write-Host "  ✅ Checksums updated!" -ForegroundColor Green
Write-Host ""

# ============================================================================
# STEP 5: Git Commit & Tag
# ============================================================================
# This tags after building, the reverse of the manual order in docs/RELEASE.md, so that the
# tagged commit already carries the checksums computed in step 4. It does not change what gets
# stamped into the binaries: GitVersion.yml pins next-version and formats every version field as
# {MajorMinorPatch}, so an untagged build and a tagged one both produce $Version exactly.
if (-not $SkipGit) {
    Write-Host "📝 Step 5: Git Commit & Tag..." -ForegroundColor Yellow

    # Show status
    Write-Host "  Git status:"
    git status --short
    Write-Host ""

    # Confirm
    $confirm = Read-Host "  Commit and tag? (y/n)"
    if ($confirm -ne 'y') {
        Write-Host "  ⏭️  Skipping git operations" -ForegroundColor Gray
    } else {
        # Stage only the release-artifact files that were modified during this
        # run. Avoid `git add -A` here because it would scoop up `dist/` (built
        # installers/portable zips) and any other untracked local state.
        git add -- `
            chocolatey\tools\chocolateyInstall.ps1 `
            winget-manifest\xmn.BetterTrumpet.installer.yaml `
            "winget-manifest\manifests\x\xmn\BetterTrumpet\$Version\xmn.BetterTrumpet.installer.yaml"

        # Commit
        $commitMsg = @"
release: bump version to $Version

- Added x64 and arm64 builds
- Added per-architecture installers and portable packages

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
"@
        git commit -m $commitMsg

        # Tag
        git tag -a "v$Version" -m "BetterTrumpet $Version"

        # Push
        Write-Host "  Pushing to origin..."
        git push origin master
        git push origin "v$Version"

        Write-Host "  ✅ Git commit & tag pushed!" -ForegroundColor Green
    }
    Write-Host ""
} else {
    Write-Host "⏭️  Skipping git operations" -ForegroundColor Gray
    Write-Host ""
}

# ============================================================================
# STEP 6: Create GitHub Release
# ============================================================================
if (-not $SkipGitHub) {
    Write-Host "🐙 Step 6: Creating GitHub Release..." -ForegroundColor Yellow

    $confirm = Read-Host "  Create GitHub release? (y/n)"
    if ($confirm -ne 'y') {
        Write-Host "  ⏭️  Skipping GitHub release" -ForegroundColor Gray
    } else {
        Write-Host "  Uploading installers and creating release..."

        # Collect all installer + portable assets
        $Assets = @()
        foreach ($arch in $Architectures) {
            $Assets += $Installers[$arch]
            $Assets += $Portables[$arch]
        }

        $missing = @($Assets | Where-Object { -not (Test-Path $_) })
        if ($missing.Count -gt 0) {
            Write-Host "❌ Missing release assets:" -ForegroundColor Red
            $missing | ForEach-Object { Write-Host "     $_" -ForegroundColor Red }
            exit 1
        }

        gh release create "v$Version" `
            $Assets `
            --title "BetterTrumpet $Version" `
            --notes-file $ReleaseNotesFile

        if ($LASTEXITCODE -ne 0) {
            Write-Host "❌ gh release create failed (exit code $LASTEXITCODE)!" -ForegroundColor Red
            exit 1
        }

        Write-Host "  ✅ GitHub release created!" -ForegroundColor Green
        Write-Host "  🔗 https://github.com/xammen/BetterTrumpet/releases/tag/v$Version" -ForegroundColor Cyan
    }
    Write-Host ""
} else {
    Write-Host "⏭️  Skipping GitHub release" -ForegroundColor Gray
    Write-Host ""
}

# ============================================================================
# STEP 7: Chocolatey Package
# ============================================================================
if (-not $SkipChocolatey) {
    Write-Host "🍫 Step 7: Chocolatey Package..." -ForegroundColor Yellow

    $confirm = Read-Host "  Build and push Chocolatey package? (y/n)"
    if ($confirm -ne 'y') {
        Write-Host "  ⏭️  Skipping Chocolatey" -ForegroundColor Gray
    } else {
        Push-Location chocolatey

        # Pack
        Write-Host "  Packing Chocolatey package..."
        choco pack

        # Push
        $pushConfirm = Read-Host "  Push to Chocolatey.org? (y/n)"
        if ($pushConfirm -eq 'y') {
            Write-Host "  Pushing to Chocolatey.org..."
            choco push "bettertrumpet.$Version.nupkg" --source https://push.chocolatey.org/
            Write-Host "  ✅ Chocolatey package pushed!" -ForegroundColor Green
        }

        Pop-Location
    }
    Write-Host ""
} else {
    Write-Host "⏭️  Skipping Chocolatey" -ForegroundColor Gray
    Write-Host ""
}

# ============================================================================
# DONE!
# ============================================================================
Write-Host "🎉 Release $Version Complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Verify GitHub release: https://github.com/xammen/BetterTrumpet/releases/tag/v$Version"
Write-Host "  2. Test auto-update from previous version → $Version (x86, x64 and arm64)"
Write-Host "  3. Close release issue with release link"
Write-Host "  4. Submit Winget PR from winget-manifest/manifests/x/xmn/BetterTrumpet/$Version/"
Write-Host ""
