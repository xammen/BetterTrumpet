# BetterTrumpet 3.0.13 Release Script
# Automates the build, packaging, and release process

param(
    [switch]$SkipBuild,
    [switch]$SkipGit,
    [switch]$SkipGitHub,
    [switch]$SkipChocolatey
)

$ErrorActionPreference = "Stop"
$Version = "3.0.13"

Write-Host "🚀 BetterTrumpet $Version Release Process" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

# ============================================================================
# STEP 1: Build Release
# ============================================================================
if (-not $SkipBuild) {
    Write-Host "📦 Step 1: Building Release..." -ForegroundColor Yellow

    # Clean
    Write-Host "  Cleaning previous build..."
    & msbuild EarTrumpet.vs15.sln /t:Clean /p:Configuration=Release /p:Platform=x86 /v:minimal

    # Build
    Write-Host "  Building Release x86..."
    & msbuild EarTrumpet.vs15.sln /t:Rebuild /p:Configuration=Release /p:Platform=x86 /m /v:minimal

    if ($LASTEXITCODE -ne 0 -and -not (Test-Path "Build\Release\BetterTrumpet.exe")) {
        Write-Host "❌ Build failed!" -ForegroundColor Red
        exit 1
    }

    Write-Host "  ✅ Build successful!" -ForegroundColor Green
    Write-Host ""
} else {
    Write-Host "⏭️  Skipping build (using existing)" -ForegroundColor Gray
    Write-Host ""
}

# ============================================================================
# STEP 2: Create Installer with Inno Setup
# ============================================================================
Write-Host "📦 Step 2: Creating Installer..." -ForegroundColor Yellow

$InnoSetupPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $InnoSetupPath)) {
    Write-Host "❌ Inno Setup not found at: $InnoSetupPath" -ForegroundColor Red
    Write-Host "   Please install Inno Setup 6 or update the path in this script" -ForegroundColor Red
    exit 1
}

Write-Host "  Running Inno Setup Compiler..."
& $InnoSetupPath installer.iss

$InstallerPath = "dist\BetterTrumpet-$Version-setup.exe"
if (-not (Test-Path $InstallerPath)) {
    Write-Host "❌ Installer not created!" -ForegroundColor Red
    exit 1
}

$InstallerSize = [math]::Round((Get-Item $InstallerPath).Length / 1MB, 2)
Write-Host "  ✅ Installer created: $InstallerSize MB" -ForegroundColor Green
Write-Host ""

# ============================================================================
# STEP 3: Calculate Checksum
# ============================================================================
Write-Host "🔐 Step 3: Calculating Checksum..." -ForegroundColor Yellow

$Checksum = (Get-FileHash -Path $InstallerPath -Algorithm SHA256).Hash
Write-Host "  SHA256: $Checksum" -ForegroundColor Cyan

# Update Chocolatey checksum
Write-Host "  Updating chocolatey checksum..."
$chocoInstall = Get-Content "chocolatey\tools\chocolateyInstall.ps1" -Raw
$chocoInstall = $chocoInstall -replace 'PLACEHOLDER_CHECKSUM_TO_BE_CALCULATED', $Checksum
Set-Content "chocolatey\tools\chocolateyInstall.ps1" $chocoInstall -NoNewline

# Update Winget checksum
Write-Host "  Updating winget checksum..."
$wingetInstaller = Get-Content "winget-pkgs\manifests\x\xmn\BetterTrumpet\$Version\xmn.BetterTrumpet.installer.yaml" -Raw
$wingetInstaller = $wingetInstaller -replace 'PLACEHOLDER_CHECKSUM_TO_BE_CALCULATED', $Checksum
Set-Content "winget-pkgs\manifests\x\xmn\BetterTrumpet\$Version\xmn.BetterTrumpet.installer.yaml" $wingetInstaller -NoNewline

Write-Host "  ✅ Checksums updated!" -ForegroundColor Green
Write-Host ""

# ============================================================================
# STEP 4: Git Commit & Tag
# ============================================================================
if (-not $SkipGit) {
    Write-Host "📝 Step 4: Git Commit & Tag..." -ForegroundColor Yellow

    # Show status
    Write-Host "  Git status:"
    git status --short
    Write-Host ""

    # Confirm
    $confirm = Read-Host "  Commit and tag? (y/n)"
    if ($confirm -ne 'y') {
        Write-Host "  ⏭️  Skipping git operations" -ForegroundColor Gray
    } else {
        # Stage all
        git add -A

        # Commit
        $commitMsg = @"
release: bump version to $Version

- Fixed backdrop rendering on startup (issue #13)
- Added device context menu
- New CLI commands: doctor, batch, volume shortcuts
- Performance improvements with 3-phase startup

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
# STEP 5: Create GitHub Release
# ============================================================================
if (-not $SkipGitHub) {
    Write-Host "🐙 Step 5: Creating GitHub Release..." -ForegroundColor Yellow

    $confirm = Read-Host "  Create GitHub release? (y/n)"
    if ($confirm -ne 'y') {
        Write-Host "  ⏭️  Skipping GitHub release" -ForegroundColor Gray
    } else {
        Write-Host "  Uploading installer and creating release..."

        gh release create "v$Version" `
            $InstallerPath `
            --title "BetterTrumpet $Version" `
            --notes-file ".claude\release-$Version-notes.md"

        Write-Host "  ✅ GitHub release created!" -ForegroundColor Green
        Write-Host "  🔗 https://github.com/xammen/BetterTrumpet/releases/tag/v$Version" -ForegroundColor Cyan
    }
    Write-Host ""
} else {
    Write-Host "⏭️  Skipping GitHub release" -ForegroundColor Gray
    Write-Host ""
}

# ============================================================================
# STEP 6: Chocolatey Package
# ============================================================================
if (-not $SkipChocolatey) {
    Write-Host "🍫 Step 6: Chocolatey Package..." -ForegroundColor Yellow

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
Write-Host "  2. Test auto-update from 3.0.12 → $Version"
Write-Host "  3. Close issue #13 with release link"
Write-Host "  4. (Optional) Submit Winget PR from winget-pkgs/manifests/x/xmn/BetterTrumpet/$Version/"
Write-Host ""
