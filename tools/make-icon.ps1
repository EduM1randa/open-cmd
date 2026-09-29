Add-Type -AssemblyName System.Drawing

function New-RoundedRect([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = [Math]::Max(1.0, $r * 2)
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc(($x + $w - $d), $y, $d, $d, 270, 90)
    $path.AddArc(($x + $w - $d), ($y + $h - $d), $d, $d, 0, 90)
    $path.AddArc($x, ($y + $h - $d), $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-IconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear([System.Drawing.Color]::FromArgb(0, 0, 0, 0))

    $inset = $size * 0.04
    $side = $size - ($inset * 2)
    $radius = $size * 0.22
    $bgPath = New-RoundedRect $inset $inset $side $side $radius
    $bg = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 20, 53, 44))
    $g.FillPath($bg, $bgPath)

    $mint = [System.Drawing.Color]::FromArgb(255, 110, 231, 183)
    $pen = New-Object System.Drawing.Pen $mint, ($size * 0.085)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

    $s = [single]$size
    $chevron = @(
        (New-Object System.Drawing.PointF ($s * 0.26), ($s * 0.32)),
        (New-Object System.Drawing.PointF ($s * 0.46), ($s * 0.50)),
        (New-Object System.Drawing.PointF ($s * 0.26), ($s * 0.68))
    )
    $g.DrawLines($pen, $chevron)
    $g.DrawLine($pen, ($s * 0.54), ($s * 0.68), ($s * 0.76), ($s * 0.68))

    $pen.Dispose()
    $bg.Dispose()
    $bgPath.Dispose()
    $g.Dispose()
    return $bmp
}

function Get-IcoImageBytes([System.Drawing.Bitmap]$bmp) {
    $w = $bmp.Width
    $h = $bmp.Height
    $xorSize = $w * $h * 4
    $maskStride = [int]([Math]::Ceiling($w / 32.0) * 4)
    $maskSize = $maskStride * $h
    $headerSize = 40
    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter $ms
    $bw.Write([uint32]40)
    $bw.Write([int32]$w)
    $bw.Write([int32]($h * 2))
    $bw.Write([uint16]1)
    $bw.Write([uint16]32)
    $bw.Write([uint32]0)
    $bw.Write([uint32]($xorSize + $maskSize))
    $bw.Write([int32]0)
    $bw.Write([int32]0)
    $bw.Write([uint32]0)
    $bw.Write([uint32]0)

    for ($y = $h - 1; $y -ge 0; $y--) {
        for ($x = 0; $x -lt $w; $x++) {
            $c = $bmp.GetPixel($x, $y)
            $bw.Write([byte]$c.B)
            $bw.Write([byte]$c.G)
            $bw.Write([byte]$c.R)
            $bw.Write([byte]$c.A)
        }
    }

    $zeros = New-Object byte[] $maskSize
    $bw.Write($zeros)
    $bw.Flush()
    Write-Output -NoEnumerate $ms.ToArray()
}

function Save-Ico($bitmaps, [string]$path) {
    $images = New-Object System.Collections.Generic.List[byte[]]
    foreach ($bmp in $bitmaps) {
        $chunk = Get-IcoImageBytes $bmp
        if ($chunk -isnot [byte[]]) { throw "La imagen del icono no salió como un bloque de bytes." }
        $images.Add($chunk)
    }
    $fs = [System.IO.File]::Create($path)
    $bw = New-Object System.IO.BinaryWriter $fs
    $bw.Write([uint16]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]$bitmaps.Count)
    $offset = 6 + (16 * $bitmaps.Count)
    for ($i = 0; $i -lt $bitmaps.Count; $i++) {
        $side = $bitmaps[$i].Width
        $dim = if ($side -ge 256) { [byte]0 } else { [byte]$side }
        $bw.Write($dim)
        $bw.Write($dim)
        $bw.Write([byte]0)
        $bw.Write([byte]0)
        $bw.Write([uint16]1)
        $bw.Write([uint16]32)
        $bw.Write([uint32]$images[$i].Length)
        $bw.Write([uint32]$offset)
        $offset += $images[$i].Length
    }
    foreach ($image in $images) { $bw.Write($image) }
    $bw.Flush()
    $bw.Dispose()
    $fs.Dispose()
}

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$assets = Join-Path $root "Assets"
New-Item -ItemType Directory -Force -Path $assets | Out-Null

function Resize-Icon([System.Drawing.Bitmap]$source, [int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::FromArgb(0, 0, 0, 0))
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.DrawImage($source, 0, 0, $size, $size)
    $g.Dispose()
    return $bmp
}

$master = New-IconBitmap 1024
$sizes = 16, 24, 32, 48, 64, 256
$bitmaps = foreach ($size in $sizes) { Resize-Icon $master $size }
$ico = Join-Path $assets "OpenCmd.ico"
Save-Ico $bitmaps $ico

$master.Dispose()
foreach ($bmp in $bitmaps) { $bmp.Dispose() }
Write-Output $ico
