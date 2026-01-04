# PowerShell script to convert icon.png to .ico and generate all MSIX assets
# Requires .NET Framework (Windows)

Add-Type -AssemblyName System.Drawing

$sourcePath = "$PSScriptRoot\icon.png"
$assetsPath = "$PSScriptRoot\EarTrumpet\Assets"
$packageAssetsPath = "$PSScriptRoot\EarTrumpet.Package\Assets"

# Load source image
$sourceImage = [System.Drawing.Image]::FromFile($sourcePath)
Write-Host "Source image loaded: $($sourceImage.Width)x$($sourceImage.Height)" -ForegroundColor Green

# Function to resize image
function Resize-Image {
    param(
        [System.Drawing.Image]$Image,
        [int]$Width,
        [int]$Height
    )
    
    $destRect = New-Object System.Drawing.Rectangle(0, 0, $Width, $Height)
    $destImage = New-Object System.Drawing.Bitmap($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    
    $destImage.SetResolution($Image.HorizontalResolution, $Image.VerticalResolution)
    
    $graphics = [System.Drawing.Graphics]::FromImage($destImage)
    $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    
    $wrapMode = New-Object System.Drawing.Imaging.ImageAttributes
    $wrapMode.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY)
    $graphics.DrawImage($Image, $destRect, 0, 0, $Image.Width, $Image.Height, [System.Drawing.GraphicsUnit]::Pixel, $wrapMode)
    
    $graphics.Dispose()
    $wrapMode.Dispose()
    
    return $destImage
}

# Function to create ICO file with multiple sizes
function Create-IcoFile {
    param(
        [System.Drawing.Image]$Image,
        [string]$OutputPath,
        [int[]]$Sizes = @(16, 24, 32, 48, 64, 128, 256)
    )
    
    $memStream = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter($memStream)
    
    # ICO header
    $writer.Write([Int16]0)      # Reserved
    $writer.Write([Int16]1)      # Type: 1 = ICO
    $writer.Write([Int16]$Sizes.Count)  # Number of images
    
    $imageData = @()
    $currentOffset = 6 + (16 * $Sizes.Count)  # Header + directory entries
    
    # Create each size
    foreach ($size in $Sizes) {
        $resized = Resize-Image -Image $Image -Width $size -Height $size
        
        # Save as PNG to memory
        $pngStream = New-Object System.IO.MemoryStream
        $resized.Save($pngStream, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngBytes = $pngStream.ToArray()
        $pngStream.Dispose()
        $resized.Dispose()
        
        $imageData += ,@{
            Size = $size
            Data = $pngBytes
            Offset = $currentOffset
        }
        
        $currentOffset += $pngBytes.Length
    }
    
    # Write directory entries
    foreach ($img in $imageData) {
        $sizeVal = if ($img.Size -ge 256) { 0 } else { $img.Size }
        $writer.Write([byte]$sizeVal)       # Width
        $writer.Write([byte]$sizeVal)       # Height
        $writer.Write([byte]0)              # Color palette
        $writer.Write([byte]0)              # Reserved
        $writer.Write([Int16]1)             # Color planes
        $writer.Write([Int16]32)            # Bits per pixel
        $writer.Write([Int32]$img.Data.Length)  # Size of image data
        $writer.Write([Int32]$img.Offset)   # Offset to image data
    }
    
    # Write image data
    foreach ($img in $imageData) {
        $writer.Write($img.Data)
    }
    
    $writer.Flush()
    
    # Save to file
    [System.IO.File]::WriteAllBytes($OutputPath, $memStream.ToArray())
    
    $writer.Dispose()
    $memStream.Dispose()
    
    Write-Host "Created ICO: $OutputPath" -ForegroundColor Cyan
}

# Function to save PNG at specific size
function Save-PngAtSize {
    param(
        [System.Drawing.Image]$Image,
        [string]$OutputPath,
        [int]$Width,
        [int]$Height = $Width
    )
    
    $resized = Resize-Image -Image $Image -Width $Width -Height $Height
    $resized.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $resized.Dispose()
    Write-Host "Created PNG: $OutputPath ($Width x $Height)" -ForegroundColor Yellow
}

Write-Host "`n=== Creating Application Icon (ICO) ===" -ForegroundColor Magenta

# Create main app icon
$icoPath = Join-Path $assetsPath "BetterTrumpet.ico"
Create-IcoFile -Image $sourceImage -OutputPath $icoPath -Sizes @(16, 24, 32, 48, 64, 128, 256)

Write-Host "`n=== Creating MSIX Package Assets ===" -ForegroundColor Magenta

# Store Logo (50x50 base)
$storeLogoSizes = @{
    "scale-100" = 50
    "scale-125" = 63
    "scale-150" = 75
    "scale-200" = 100
    "scale-400" = 200
}

foreach ($scale in $storeLogoSizes.Keys) {
    $size = $storeLogoSizes[$scale]
    $path = Join-Path $packageAssetsPath "StoreLogo.$scale.png"
    Save-PngAtSize -Image $sourceImage -OutputPath $path -Width $size
}

# Square44x44Logo (44x44 base)
$square44Sizes = @{
    "scale-100" = 44
    "scale-125" = 55
    "scale-150" = 66
    "scale-200" = 88
    "scale-400" = 176
}

foreach ($scale in $square44Sizes.Keys) {
    $size = $square44Sizes[$scale]
    $path = Join-Path $packageAssetsPath "Square44x44Logo.$scale.png"
    Save-PngAtSize -Image $sourceImage -OutputPath $path -Width $size
}

# Square44x44Logo target sizes
$targetSizes = @(16, 24, 32, 48, 256)
foreach ($size in $targetSizes) {
    $path = Join-Path $packageAssetsPath "Square44x44Logo.targetsize-$size.png"
    Save-PngAtSize -Image $sourceImage -OutputPath $path -Width $size
    
    # Also create altform-unplated versions (same image, no plate background)
    $pathUnplated = Join-Path $packageAssetsPath "Square44x44Logo.altform-unplated_targetsize-$size.png"
    Save-PngAtSize -Image $sourceImage -OutputPath $pathUnplated -Width $size
}

# Square150x150Logo (150x150 base)
$square150Sizes = @{
    "scale-100" = 150
    "scale-125" = 188
    "scale-150" = 225
    "scale-200" = 300
    "scale-400" = 600
}

foreach ($scale in $square150Sizes.Keys) {
    $size = $square150Sizes[$scale]
    $path = Join-Path $packageAssetsPath "Square150x150Logo.$scale.png"
    Save-PngAtSize -Image $sourceImage -OutputPath $path -Width $size
}

# SmallTile / Square71x71Logo (71x71 base)
$smallTileSizes = @{
    "scale-100" = 71
    "scale-125" = 89
    "scale-150" = 107
    "scale-200" = 142
    "scale-400" = 284
}

foreach ($scale in $smallTileSizes.Keys) {
    $size = $smallTileSizes[$scale]
    $path = Join-Path $packageAssetsPath "SmallTile.$scale.png"
    Save-PngAtSize -Image $sourceImage -OutputPath $path -Width $size
}

# LargeTile / Square310x310Logo (310x310 base)
$largeTileSizes = @{
    "scale-100" = 310
    "scale-125" = 388
    "scale-150" = 465
    "scale-200" = 620
    "scale-400" = 1240
}

foreach ($scale in $largeTileSizes.Keys) {
    $size = $largeTileSizes[$scale]
    $path = Join-Path $packageAssetsPath "LargeTile.$scale.png"
    Save-PngAtSize -Image $sourceImage -OutputPath $path -Width $size
}

# Wide310x150Logo (310x150 base)
$wideTileSizes = @{
    "scale-100" = @(310, 150)
    "scale-125" = @(388, 188)
    "scale-150" = @(465, 225)
    "scale-200" = @(620, 300)
    "scale-400" = @(1240, 600)
}

foreach ($scale in $wideTileSizes.Keys) {
    $dims = $wideTileSizes[$scale]
    $width = $dims[0]
    $height = $dims[1]
    $path = Join-Path $packageAssetsPath "Wide310x150Logo.$scale.png"
    
    # Create wide tile with centered icon
    $destImage = New-Object System.Drawing.Bitmap($width, $height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($destImage)
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    
    # Center the icon in the wide tile
    $iconSize = [Math]::Min($height - 20, $height)
    $x = ($width - $iconSize) / 2
    $y = ($height - $iconSize) / 2
    $graphics.DrawImage($sourceImage, $x, $y, $iconSize, $iconSize)
    
    $graphics.Dispose()
    $destImage.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $destImage.Dispose()
    
    Write-Host "Created PNG: $path ($width x $height)" -ForegroundColor Yellow
}

# SplashScreen (620x300 base)
$splashSizes = @{
    "scale-100" = @(620, 300)
    "scale-125" = @(775, 375)
    "scale-150" = @(930, 450)
    "scale-200" = @(1240, 600)
    "scale-400" = @(2480, 1200)
}

foreach ($scale in $splashSizes.Keys) {
    $dims = $splashSizes[$scale]
    $width = $dims[0]
    $height = $dims[1]
    $path = Join-Path $packageAssetsPath "SplashScreen.$scale.png"
    
    # Create splash with centered icon
    $destImage = New-Object System.Drawing.Bitmap($width, $height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($destImage)
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    
    $iconSize = [Math]::Min($height - 40, $height * 0.8)
    $x = ($width - $iconSize) / 2
    $y = ($height - $iconSize) / 2
    $graphics.DrawImage($sourceImage, $x, $y, $iconSize, $iconSize)
    
    $graphics.Dispose()
    $destImage.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $destImage.Dispose()
    
    Write-Host "Created PNG: $path ($width x $height)" -ForegroundColor Yellow
}

$sourceImage.Dispose()

Write-Host "`n=== All icons generated successfully! ===" -ForegroundColor Green
Write-Host "Application icon: $icoPath"
Write-Host "Package assets: $packageAssetsPath"
