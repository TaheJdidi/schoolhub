# Fabrique les icônes « SH » de l'application, d'après le logo de la maquette, dans public/icones/.
# À lancer avec Windows PowerShell 5.1, depuis frontend/ :
#   powershell -NoProfile -ExecutionPolicy Bypass -File outils/creer-icones.ps1

Add-Type -AssemblyName System.Drawing

$dossier = Join-Path $PSScriptRoot '..\public\icones'
New-Item -ItemType Directory -Force $dossier | Out-Null
$bleu = [System.Drawing.ColorTranslator]::FromHtml('#1B5DA4')

# Arrondie : coins arrondis comme le logo de la maquette (rayon de 0,26 × la taille), « SH » à 0,40 × la taille.
# Pleine : le fond remplit tout le carré. C'est ce qu'attendent Android (version adaptée, dont il découpe
# lui-même la forme : « SH » reste dans la zone sûre, à 0,30 × la taille) et l'iPhone (il arrondit lui-même
# les coins, et remplirait de noir des coins transparents).
function Creer-Icone([int]$taille, [string]$nom, [bool]$pleine, [double]$proportion) {
    $image = New-Object System.Drawing.Bitmap $taille, $taille
    $dessin = [System.Drawing.Graphics]::FromImage($image)
    $dessin.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $dessin.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $dessin.Clear([System.Drawing.Color]::Transparent)
    $fond = New-Object System.Drawing.SolidBrush $bleu

    if ($pleine) {
        $dessin.FillRectangle($fond, 0, 0, $taille, $taille)
    } else {
        $d = [int]($taille * 0.26) * 2
        $forme = New-Object System.Drawing.Drawing2D.GraphicsPath
        $forme.AddArc(0, 0, $d, $d, 180, 90)
        $forme.AddArc($taille - $d, 0, $d, $d, 270, 90)
        $forme.AddArc($taille - $d, $taille - $d, $d, $d, 0, 90)
        $forme.AddArc(0, $taille - $d, $d, $d, 90, 90)
        $forme.CloseFigure()
        $dessin.FillPath($fond, $forme)
        $forme.Dispose()
    }

    $police = New-Object System.Drawing.Font 'Segoe UI Semibold', ($taille * $proportion), ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
    $format = New-Object System.Drawing.StringFormat
    $format.Alignment = [System.Drawing.StringAlignment]::Center
    $format.LineAlignment = [System.Drawing.StringAlignment]::Center
    $zone = New-Object System.Drawing.RectangleF 0, 0, $taille, $taille
    $dessin.DrawString('SH', $police, [System.Drawing.Brushes]::White, $zone, $format)

    $image.Save((Join-Path $dossier $nom), [System.Drawing.Imaging.ImageFormat]::Png)
    $police.Dispose(); $format.Dispose(); $fond.Dispose(); $dessin.Dispose(); $image.Dispose()
}

Creer-Icone 32 'sh-32.png' $false 0.40
Creer-Icone 180 'sh-180.png' $true 0.40
Creer-Icone 192 'sh-192.png' $false 0.40
Creer-Icone 512 'sh-512.png' $false 0.40
Creer-Icone 512 'sh-512-adaptee.png' $true 0.30
