# Generates the Windows application icon (Stage 18) — windows/src/Mitosis.Windows.App/app.ico
# — from the product's icon artwork at Assets/Brand/AppIcon.icon/mito.png.
#
# Rules honored (docs/WINDOWS-RELEASE.md):
#   - Assets/ is READ-ONLY: the source PNG is opened, never written or moved.
#   - The repository is Mitosis itself (not a fork), so reusing the product's
#     artwork for the Windows port is the established permission per
#     docs/WINDOWS-PORT.md section 8; no publisher or signing identity is added.
#   - Classic all-BMP 32-bpp ICO entries (16..256) for maximum tool-shell
#     compatibility; no PNG-in-ICO except at 256 where BMP is still used.
param(
    [string]$SourcePath = (Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) "Assets\Brand\AppIcon.icon\Assets\mito.png"),
    [string]$OutputPath = (Join-Path (Split-Path -Parent $PSScriptRoot) "src\Mitosis.Windows.App\app.ico")
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

if (-not (Test-Path $SourcePath)) { throw "Icon source not found: $SourcePath (Assets/ must stay in place)" }
$sizes = 16, 24, 32, 48, 64, 128, 256
$source = [System.Drawing.Image]::FromFile((Resolve-Path $SourcePath).Path)
try {
    $entries = @()
    foreach ($s in $sizes) {
        $bmp = New-Object System.Drawing.Bitmap($s, $s)
        try {
            $g = [System.Drawing.Graphics]::FromImage($bmp)
            try {
                $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
                $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                $g.Clear([System.Drawing.Color]::Transparent)
                $g.DrawImage($source, (New-Object System.Drawing.Rectangle(0, 0, $s, $s)))
            } finally { $g.Dispose() }
        } finally {
            # Keep the bitmap alive until data is read below.
            $rect = New-Object System.Drawing.Rectangle(0, 0, $s, $s)
            $locked = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
                [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
            $stride = $locked.Stride
            $pixels = New-Object byte[] ($s * $s * 4)
            [System.Runtime.InteropServices.Marshal]::Copy($locked.Scan0, $pixels, 0, $pixels.Length)
            $bmp.UnlockBits($locked)
            $bmp.Dispose()
        }
        # BITMAPINFOHEADER (40 bytes) for an icon: height is doubled (XOR + AND mask).
        $header = New-Object byte[] 40
        [BitConverter]::GetBytes([UInt32]40).CopyTo($header, 0)      # biSize
        [BitConverter]::GetBytes([Int32]$s).CopyTo($header, 4)       # biWidth
        [BitConverter]::GetBytes([Int32]($s * 2)).CopyTo($header, 8) # biHeight
        [BitConverter]::GetBytes([UInt16]1).CopyTo($header, 12)      # biPlanes
        [BitConverter]::GetBytes([UInt16]32).CopyTo($header, 14)     # biBitCount
        [BitConverter]::GetBytes([UInt32]($s * $s * 4)).CopyTo($header, 20) # biSizeImage
        # The pixels are top-down; icon BMP data must be bottom-up.
        $rows = New-Object byte[] ($s * $s * 4)
        for ($row = 0; $row -lt $s; $row++) {
            $src = ($s - 1 - $row) * $s * 4
            [Array]::Copy($pixels, $src, $rows, $row * $s * 4, $s * 4)
        }
        # AND mask: 1 bpp, all zeros (alpha already carries opacity), rows aligned
        # to 4 bytes and stored bottom-up like the pixel rows.
        $maskStride = [int]([Math]::Ceiling(($s + 31) / 32.0)) * 4
        $mask = New-Object byte[] ($maskStride * $s)
        # PowerShell's '+' on byte arrays boxes into Object[], which BinaryWriter
        # serializes wrongly; assemble the body with an explicit copy instead.
        $body = New-Object byte[] ($header.Length + $rows.Length + $mask.Length)
        [Array]::Copy($header, $body, $header.Length)
        [Array]::Copy($rows, 0, $body, $header.Length, $rows.Length)
        [Array]::Copy($mask, 0, $body, $header.Length + $rows.Length, $mask.Length)
        $entries += [PsCustomObject]@{ Width = $s; Bmp = $body }
    }

    # ICO container: ICONDIR (6 bytes) + 16-byte directory per entry + bodies.
    $outFile = [System.IO.Path]::GetFullPath($OutputPath)
    $ms = New-Object System.IO.MemoryStream
    try {
        $writer = New-Object System.IO.BinaryWriter($ms)
        $writer.Write([UInt16]0); $writer.Write([UInt16]1); $writer.Write([UInt16]$entries.Count)
        $bodyOffset = 6 + 16 * $entries.Count
        foreach ($e in $entries) {
            $writer.Write([Byte]$(if ($e.Width -eq 256) { 0 } else { $e.Width }))
            $writer.Write([Byte]$(if ($e.Width -eq 256) { 0 } else { $e.Width }))
            $writer.Write([Byte]0); $writer.Write([Byte]0)          # color count, reserved
            $writer.Write([UInt16]1)                                 # planes
            $writer.Write([UInt16]32)                                # bitcount
            $writer.Write([UInt32]$e.Bmp.Length)
            $writer.Write([UInt32]$bodyOffset)
            $bodyOffset += $e.Bmp.Length
        }
        foreach ($e in $entries) { $writer.Write($e.Bmp) }
        $writer.Flush()
        [System.IO.File]::WriteAllBytes($outFile, $ms.ToArray())
    } finally { $ms.Dispose() }
} finally { $source.Dispose() }

# Verify what was written: correct magic, entry count, and reasonable size.
$bytes = [System.IO.File]::ReadAllBytes((Resolve-Path $OutputPath).Path)
if ($bytes[0] -ne 0 -or $bytes[1] -ne 0 -or $bytes[2] -ne 1 -or $bytes[3] -ne 0) {
    throw "app.ico magic check failed"
}
if ([BitConverter]::ToUInt16($bytes, 4) -ne $sizes.Count) { throw "app.ico entry count mismatch" }
Write-Host ("app.ico written: {0} entries, {1:N0} bytes" -f $sizes.Count, $bytes.Length)
