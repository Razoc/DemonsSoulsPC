# Draws Demon's Souls PC's own icon (a gold sword, point down, on a dark rounded square) so the public app and
# launcher carry no game artwork. Writes DemonsSoulsPC.App\Resources\dspc.ico and dspc.png.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$out = Join-Path $root 'DemonsSoulsPC.App\Resources'

function Draw([int]$n) {
    $bmp = New-Object Drawing.Bitmap $n, $n
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([Drawing.Color]::Transparent)
    $r = [Math]::Max(2, [int]($n * 0.18))
    $path = New-Object Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0, 0, 2 * $r, 2 * $r, 180, 90)
    $path.AddArc($n - 1 - 2 * $r, 0, 2 * $r, 2 * $r, 270, 90)
    $path.AddArc($n - 1 - 2 * $r, $n - 1 - 2 * $r, 2 * $r, 2 * $r, 0, 90)
    $path.AddArc(0, $n - 1 - 2 * $r, 2 * $r, 2 * $r, 90, 90)
    $path.CloseFigure()
    $g.FillPath((New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(255, 24, 22, 20))), $path)
    $gold = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(255, 214, 178, 102))
    $dark = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(255, 150, 118, 60))
    function P([double]$x, [double]$y) { New-Object Drawing.PointF ([float]($n * $x)), ([float]($n * $y)) }
    # Lame (pointe en bas) avec son arête, garde, poignée, pommeau.
    $g.FillPolygon($gold, [Drawing.PointF[]]@((P 0.455 0.33), (P 0.545 0.33), (P 0.545 0.78), (P 0.5 0.9), (P 0.455 0.78)))
    $g.FillPolygon($dark, [Drawing.PointF[]]@((P 0.493 0.34), (P 0.507 0.34), (P 0.507 0.78), (P 0.5 0.86), (P 0.493 0.78)))
    $g.FillRectangle($gold, [float]($n * 0.26), [float]($n * 0.27), [float]($n * 0.48), [float]([Math]::Max(1, $n * 0.06)))
    $g.FillRectangle($dark, [float]($n * 0.47), [float]($n * 0.13), [float]($n * 0.06), [float]($n * 0.14))
    $g.FillEllipse($gold, [float]($n * 0.45), [float]($n * 0.07), [float]($n * 0.10), [float]($n * 0.10))
    $g.Dispose()
    return $bmp
}

function Png($bmp) {
    $ms = New-Object IO.MemoryStream
    $bmp.Save($ms, [Drawing.Imaging.ImageFormat]::Png)
    return ,$ms.ToArray()
}

$big = Draw 192
$big.Save((Join-Path $out 'dspc.png'), [Drawing.Imaging.ImageFormat]::Png)
$release = Join-Path (Split-Path $root -Parent) 'Release'
New-Item -ItemType Directory -Force $release | Out-Null
(Draw 1024).Save((Join-Path $release 'icone-1024.png'), [Drawing.Imaging.ImageFormat]::Png)
$sizes = @(256, 64, 48, 32, 16)
$pngs = @($sizes | ForEach-Object { ,(Png (Draw $_)) })
$ico = New-Object IO.MemoryStream
$w = New-Object IO.BinaryWriter $ico
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $n = $sizes[$i]; $b = if ($n -ge 256) { 0 } else { $n }
    $w.Write([byte]$b); $w.Write([byte]$b); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$pngs[$i].Length); $w.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $w.Write($p) }
$w.Flush()
[IO.File]::WriteAllBytes((Join-Path $out 'dspc.ico'), $ico.ToArray())
"wrote dspc.ico, dspc.png and ..\Release\icone-1024.png"
