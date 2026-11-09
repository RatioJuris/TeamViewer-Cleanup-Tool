# Draws the app icon (a white shield with a check mark) and writes app.ico with PNG frames.
param([string]$Out = "$PSScriptRoot\..\src\TeamViewerCleanup\app.ico")

Add-Type -AssemblyName System.Drawing
$sizes = 16, 24, 32, 48, 64, 256
$frames = @()

foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)
    $k = $s / 64.0

    # Rounded square background
    $r = 14 * $k
    $bg = New-Object System.Drawing.Drawing2D.GraphicsPath
    $bg.AddArc(0, 0, 2 * $r, 2 * $r, 180, 90)
    $bg.AddArc($s - 2 * $r - 1, 0, 2 * $r, 2 * $r, 270, 90)
    $bg.AddArc($s - 2 * $r - 1, $s - 2 * $r - 1, 2 * $r, 2 * $r, 0, 90)
    $bg.AddArc(0, $s - 2 * $r - 1, 2 * $r, 2 * $r, 90, 90)
    $bg.CloseFigure()
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.Point 0, 0), (New-Object System.Drawing.Point $s, $s), ([System.Drawing.Color]::FromArgb(14, 99, 156)), ([System.Drawing.Color]::FromArgb(15, 31, 77))
    $g.FillPath($brush, $bg)

    # Shield
    $shield = New-Object System.Drawing.Drawing2D.GraphicsPath
    $pts = @(
        (New-Object System.Drawing.PointF (32 * $k), (11 * $k)),
        (New-Object System.Drawing.PointF (50 * $k), (17 * $k)),
        (New-Object System.Drawing.PointF (50 * $k), (31 * $k))
    )
    $shield.AddLines($pts)
    $shield.AddBezier((50 * $k), (31 * $k), (50 * $k), (43 * $k), (42 * $k), (50 * $k), (32 * $k), (55 * $k))
    $shield.AddBezier((32 * $k), (55 * $k), (22 * $k), (50 * $k), (14 * $k), (43 * $k), (14 * $k), (31 * $k))
    $shield.AddLines(@((New-Object System.Drawing.PointF (14 * $k), (31 * $k)), (New-Object System.Drawing.PointF (14 * $k), (17 * $k))))
    $shield.CloseFigure()
    $g.FillPath([System.Drawing.Brushes]::White, $shield)

    # Check mark
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(22, 128, 61)), ([Math]::Max(1.5, 5 * $k))
    $pen.StartCap = 'Round'; $pen.EndCap = 'Round'; $pen.LineJoin = 'Round'
    $g.DrawLines($pen, @(
        (New-Object System.Drawing.PointF (23 * $k), (33 * $k)),
        (New-Object System.Drawing.PointF (30 * $k), (40 * $k)),
        (New-Object System.Drawing.PointF (42 * $k), (26 * $k))
    ))

    $g.Dispose()
    if ($s -ge 256) {
        # Large frame: PNG-compressed (supported by Windows Vista and later).
        $ms = [System.IO.MemoryStream]::new()
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $data = $ms.ToArray()
        [System.IO.File]::WriteAllBytes([System.IO.Path]::GetFullPath("$PSScriptRoot\..\docs\icon.png"), $data)
    } else {
        # Small frames: classic 32-bit DIB (BITMAPINFOHEADER + bottom-up BGRA + empty AND mask).
        $dib = [System.IO.MemoryStream]::new()
        $bw = [System.IO.BinaryWriter]::new($dib)
        $bw.Write([uint32]40); $bw.Write([int32]$s); $bw.Write([int32]($s * 2))
        $bw.Write([uint16]1); $bw.Write([uint16]32); $bw.Write([uint32]0)
        $bw.Write([uint32]($s * $s * 4)); $bw.Write([int32]0); $bw.Write([int32]0); $bw.Write([uint32]0); $bw.Write([uint32]0)
        for ($y = $s - 1; $y -ge 0; $y--) {
            for ($x = 0; $x -lt $s; $x++) {
                $c = $bmp.GetPixel($x, $y)
                $bw.Write([byte]$c.B); $bw.Write([byte]$c.G); $bw.Write([byte]$c.R); $bw.Write([byte]$c.A)
            }
        }
        $maskRow = [int]([Math]::Ceiling($s / 32.0) * 4)
        $bw.Write((New-Object byte[] ($maskRow * $s)))
        $bw.Flush()
        $data = $dib.ToArray()
    }
    $frames += , @($s, $data)
    $bmp.Dispose()
}

# ICO container: header, directory entries, then PNG data.
$icoStream = [System.IO.MemoryStream]::new()
$w = [System.IO.BinaryWriter]::new($icoStream)
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$frames.Count)
$offset = 6 + 16 * $frames.Count
foreach ($f in $frames) {
    $size = $f[0]; $data = $f[1]
    $dim = if ($size -ge 256) { 0 } else { $size }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$data.Length); $w.Write([uint32]$offset)
    $offset += $data.Length
}
foreach ($f in $frames) { $w.Write([byte[]]$f[1]) }
$w.Flush()
$target = [System.IO.Path]::GetFullPath($Out)
[System.IO.File]::WriteAllBytes($target, $icoStream.ToArray())
"Wrote $target ($($icoStream.Length) bytes, $($frames.Count) sizes)"
