# Nesto#457 - Generador de iconos del ribbon (tanda 1: los 7 que van prestados de Cliente.png)
# Ejecutar con: powershell.exe -STA -File GenerarIconos.ps1
# Estilo: plano, azul corporativo + blanco/gris, fondo transparente (como Familias.png)

Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase

$outDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Paleta (aproximada a Cliente.png / Familias.png)
$azul       = New-Object Windows.Media.SolidColorBrush ([Windows.Media.Color]::FromRgb(0x2B, 0x62, 0x86))
$azulOscuro = New-Object Windows.Media.SolidColorBrush ([Windows.Media.Color]::FromRgb(0x1D, 0x47, 0x63))
$gris       = New-Object Windows.Media.SolidColorBrush ([Windows.Media.Color]::FromRgb(0xA9, 0xB0, 0xB6))
$grisOscuro = New-Object Windows.Media.SolidColorBrush ([Windows.Media.Color]::FromRgb(0x7E, 0x87, 0x8E))
$blanco     = [Windows.Media.Brushes]::White
$azul.Freeze(); $azulOscuro.Freeze(); $gris.Freeze(); $grisOscuro.Freeze()

function New-Pen($brush, $grosor, [switch]$Redondo) {
    $pen = New-Object Windows.Media.Pen $brush, $grosor
    if ($Redondo) {
        $pen.StartLineCap = 'Round'; $pen.EndLineCap = 'Round'; $pen.LineJoin = 'Round'
    }
    $pen.Freeze()
    return $pen
}

function Texto($dc, [string]$texto, $brush, [double]$tamano, [double]$x, [double]$y) {
    $tf = New-Object Windows.Media.Typeface (New-Object Windows.Media.FontFamily 'Segoe UI'), 'Normal', 'Bold', 'Normal'
    $ft = New-Object Windows.Media.FormattedText $texto, ([Globalization.CultureInfo]::InvariantCulture),
        ([Windows.FlowDirection]::LeftToRight), $tf, $tamano, $brush, 1.0
    # x,y = centro deseado
    $punto = New-Object Windows.Point ($x - $ft.Width / 2), ($y - $ft.Height / 2)
    $dc.DrawText($ft, $punto)
}

function Geo([string]$path) { return [Windows.Media.Geometry]::Parse($path) }

function New-Icon([string]$nombre, [scriptblock]$dibujo) {
    $dv = New-Object Windows.Media.DrawingVisual
    $dc = $dv.RenderOpen()
    & $dibujo $dc
    $dc.Close()
    $rtb = New-Object Windows.Media.Imaging.RenderTargetBitmap 64, 64, 96, 96, ([Windows.Media.PixelFormats]::Pbgra32)
    $rtb.Render($dv)
    $enc = New-Object Windows.Media.Imaging.PngBitmapEncoder
    $enc.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($rtb))
    $fs = [IO.File]::Create((Join-Path $outDir $nombre))
    $enc.Save($fs)
    $fs.Close()
    Write-Host "OK $nombre"
}

function Rect($x, $y, $w, $h) { New-Object Windows.Rect $x, $y, $w, $h }
function Punto($x, $y) { New-Object Windows.Point $x, $y }

# ============================================================================
# 1. Alquileres: un aparato de cabina con el ciclo de renovacion encima
# ============================================================================
New-Icon 'Alquileres.png' {
    param($dc)
    # Cuerpo del aparato
    $dc.DrawRoundedRectangle($azul, $null, (Rect 10 6 34 40), 4, 4)
    # Pantalla
    $dc.DrawRoundedRectangle($blanco, $null, (Rect 15 11 24 13), 2, 2)
    # Mandos
    $dc.DrawEllipse($blanco, $null, (Punto 21 33), 3.5, 3.5)
    $dc.DrawEllipse($blanco, $null, (Punto 33 33), 3.5, 3.5)
    # Patas
    $dc.DrawRectangle($azulOscuro, $null, (Rect 14 46 5 6))
    $dc.DrawRectangle($azulOscuro, $null, (Rect 35 46 5 6))
    # Reloj: el alquiler es uso por tiempo
    $dc.DrawEllipse($azulOscuro, $null, (Punto 48 46), 14, 14)
    $penReloj = New-Pen $blanco 3 -Redondo
    $dc.DrawEllipse($null, $penReloj, (Punto 48 46), 8, 8)
    $dc.DrawLine($penReloj, (Punto 48 46), (Punto 48 40.5))
    $dc.DrawLine($penReloj, (Punto 48 46), (Punto 52.5 46))
}

# ============================================================================
# 2. Remesas: fajo de recibos que sale hacia el banco (flecha)
# ============================================================================
New-Icon 'Remesas.png' {
    param($dc)
    # Recibo de atras
    $dc.DrawRectangle($gris, $null, (Rect 14 8 26 36))
    # Recibo de delante
    $penDoc = New-Pen $azul 2.5
    $dc.DrawRectangle($blanco, $penDoc, (Rect 8 14 26 36))
    # Lineas del recibo
    $penLinea = New-Pen $azul 2.5 -Redondo
    $dc.DrawLine($penLinea, (Punto 13 23), (Punto 29 23))
    $dc.DrawLine($penLinea, (Punto 13 30), (Punto 29 30))
    $dc.DrawLine($penLinea, (Punto 13 37), (Punto 24 37))
    # Euro en el recibo
    Texto $dc ([char]0x20AC) $azul 13 27 41
    # Flecha de salida (la remesa se envia)
    $dc.DrawRectangle($azulOscuro, $null, (Rect 38 28 12 8))
    $dc.DrawGeometry($azulOscuro, $null, (Geo 'M 50 20 L 62 32 L 50 44 Z'))
}

# ============================================================================
# 3. Agencias: paquete con etiqueta de codigo de barras
# ============================================================================
New-Icon 'Agencias.png' {
    param($dc)
    # Caja
    $dc.DrawRectangle($azul, $null, (Rect 8 20 48 36))
    # Solapas superiores
    $dc.DrawGeometry($azulOscuro, $null, (Geo 'M 8 20 L 20 8 L 44 8 L 56 20 Z'))
    # Cinta vertical
    $dc.DrawRectangle($blanco, $null, (Rect 28 20 8 36))
    $dc.DrawRectangle($gris, $null, (Rect 28 8 8 12))
    # Etiqueta con codigo de barras
    $dc.DrawRectangle($blanco, $null, (Rect 38 34 16 16))
    foreach ($linea in @(41, 43.5, 46, 49, 51.5)) {
        $alto = if ($linea -in @(43.5, 49)) { 9 } else { 12 }
        $dc.DrawRectangle($azulOscuro, $null, (Rect $linea 36 1.5 $alto))
    }
}

# ============================================================================
# 4. Mant. agencias: el mismo paquete con un engranaje encima
# ============================================================================
New-Icon 'AgenciasMantenimiento.png' {
    param($dc)
    # Caja (mas pequena, deja sitio al engranaje)
    $dc.DrawRectangle($azul, $null, (Rect 6 22 40 32))
    $dc.DrawGeometry($azulOscuro, $null, (Geo 'M 6 22 L 16 12 L 36 12 L 46 22 Z'))
    $dc.DrawRectangle($blanco, $null, (Rect 22 22 8 32))
    $dc.DrawRectangle($gris, $null, (Rect 22 12 8 10))
    # Engranaje en badge
    $dc.DrawEllipse($azulOscuro, $null, (Punto 48 44), 15, 15)
    $centro = Punto 48 44
    # Dientes
    for ($i = 0; $i -lt 8; $i++) {
        $angulo = $i * 45
        $transformada = New-Object Windows.Media.RotateTransform $angulo, 48, 44
        $dc.PushTransform($transformada)
        $dc.DrawRectangle($blanco, $null, (Rect 46 33.5 4 5))
        $dc.Pop()
    }
    # Cuerpo del engranaje y agujero
    $dc.DrawEllipse($blanco, $null, $centro, 7.5, 7.5)
    $dc.DrawEllipse($azulOscuro, $null, $centro, 3.5, 3.5)
}

# ============================================================================
# 5. ClientesVendedor: los clientes de un vendedor (dos personas)
# ============================================================================
New-Icon 'ClientesVendedor.png' {
    param($dc)
    # Persona de atras (gris)
    $dc.DrawEllipse($gris, $null, (Punto 24 17), 9, 9)
    $dc.DrawGeometry($gris, $null, (Geo 'M 8 44 C 8 32 14 28 24 28 C 34 28 40 32 40 44 Z'))
    # Persona de delante (azul)
    $dc.DrawEllipse($grisOscuro, $null, (Punto 42 27), 9.5, 9.5)
    $dc.DrawGeometry($azul, $null, (Geo 'M 25 58 C 25 44 31 39 42 39 C 53 39 59 44 59 58 Z'))
}

# ============================================================================
# 6. PlanesVentajas: caja de regalo
# ============================================================================
New-Icon 'PlanesVentajas.png' {
    param($dc)
    # Caja
    $dc.DrawRectangle($azul, $null, (Rect 12 34 40 24))
    # Tapa (sobresale de la caja)
    $dc.DrawRectangle($azulOscuro, $null, (Rect 8 24 48 10))
    # Cinta vertical (solo sobre caja y tapa, que el blanco no pisa la transparencia)
    $dc.DrawRectangle($blanco, $null, (Rect 28 24 8 34))
    # Lazo: dos bucles azul oscuro sobre la transparencia (visibles) y nudo azul
    $bucleIzq = New-Object Windows.Media.RotateTransform -25, 25, 17
    $dc.PushTransform($bucleIzq)
    $dc.DrawEllipse($azulOscuro, $null, (Punto 25 17), 8, 5.5)
    $dc.Pop()
    $bucleDer = New-Object Windows.Media.RotateTransform 25, 39, 17
    $dc.PushTransform($bucleDer)
    $dc.DrawEllipse($azulOscuro, $null, (Punto 39 17), 8, 5.5)
    $dc.Pop()
    $dc.DrawEllipse($azul, $null, (Punto 32 20), 4, 4)
}

# ============================================================================
# 7. Comisiones: la bolsa del dinero
# ============================================================================
New-Icon 'Comisiones.png' {
    param($dc)
    # Cuerpo de la bolsa
    $dc.DrawGeometry($azul, $null, (Geo 'M 26 18 C 12 24 6 36 8 46 C 10 56 20 60 32 60 C 44 60 54 56 56 46 C 58 36 52 24 38 18 Z'))
    # Nudo
    $dc.DrawGeometry($azulOscuro, $null, (Geo 'M 24 6 L 40 6 L 37 16 L 27 16 Z'))
    $dc.DrawRectangle($grisOscuro, $null, (Rect 25 15 14 4))
    # Euro
    Texto $dc ([char]0x20AC) $blanco 26 32 41
}

# ============================================================================
# TANDA 2 - Modulos/Cliente (los 4 que van prestados de crear_cliente.png)
# ============================================================================

# NIF incorrectos: tarjeta de identidad con aviso
New-Icon 'NifIncorrectos.png' {
    param($dc)
    $penTarjeta = New-Pen $azul 2.5
    $dc.DrawRoundedRectangle($blanco, $penTarjeta, (Rect 4 14 48 32), 3, 3)
    # Avatar en la tarjeta
    $dc.DrawEllipse($azul, $null, (Punto 15 25), 5, 5)
    $dc.DrawGeometry($azul, $null, (Geo 'M 7 42 C 7 34 10 32 15 32 C 20 32 23 34 23 42 Z'))
    # Lineas (el NIF)
    $penLinea = New-Pen $azul 2.5 -Redondo
    $dc.DrawLine($penLinea, (Punto 28 24), (Punto 47 24))
    $dc.DrawLine($penLinea, (Punto 28 31), (Punto 43 31))
    # Badge de aviso
    $dc.DrawEllipse($azulOscuro, $null, (Punto 48 46), 13, 13)
    Texto $dc '!' $blanco 22 48 45
}

# Codigos Postales: chincheta de mapa
New-Icon 'CodigosPostales.png' {
    param($dc)
    $dc.DrawGeometry($azul, $null, (Geo 'M 32 58 C 21 42 15 34 15 23 A 17 17 0 1 1 49 23 C 49 34 43 42 32 58 Z'))
    $dc.DrawEllipse($blanco, $null, (Punto 32 23), 7.5, 7.5)
}

# Modelo 347: el impreso con su numero
New-Icon 'Modelo347.png' {
    param($dc)
    $penDoc = New-Pen $azul 2.5
    $dc.DrawRectangle($blanco, $penDoc, (Rect 12 6 40 52))
    # Esquina doblada
    $dc.DrawGeometry($azul, $null, (Geo 'M 40 6 L 52 18 L 40 18 Z'))
    $penLinea = New-Pen $gris 2.5 -Redondo
    $dc.DrawLine($penLinea, (Punto 18 16), (Punto 34 16))
    $dc.DrawLine($penLinea, (Punto 18 23), (Punto 46 23))
    Texto $dc '347' $azul 17 32 40
}

# Extracto Cliente: los movimientos con la lupa de consultar
New-Icon 'ExtractoCliente.png' {
    param($dc)
    $penDoc = New-Pen $azul 2.5
    $dc.DrawRectangle($blanco, $penDoc, (Rect 8 6 40 52))
    $penLinea = New-Pen $azul 2.5 -Redondo
    foreach ($y in @(15, 23, 31, 39)) {
        $dc.DrawLine($penLinea, (Punto 14 $y), (Punto 42 $y))
    }
    # Lupa
    $dc.DrawEllipse($azulOscuro, $null, (Punto 46 46), 14, 14)
    $penLupa = New-Pen $blanco 3 -Redondo
    $dc.DrawEllipse($null, $penLupa, (Punto 43.5 43.5), 5.5, 5.5)
    $dc.DrawLine($penLupa, (Punto 47.5 47.5), (Punto 53 53))
}

# ============================================================================
# TANDA 3 - CanalesExternos (los 2 que van prestados de CanalesExternosPagos.png)
# ============================================================================

# Facturas: el documento con el euro
New-Icon 'CanalesExternosFacturas.png' {
    param($dc)
    $penDoc = New-Pen $azul 2.5
    $dc.DrawRectangle($blanco, $penDoc, (Rect 10 6 38 52))
    $dc.DrawGeometry($azul, $null, (Geo 'M 36 6 L 48 18 L 36 18 Z'))
    $penLinea = New-Pen $gris 2.5 -Redondo
    $dc.DrawLine($penLinea, (Punto 16 16), (Punto 30 16))
    $dc.DrawLine($penLinea, (Punto 16 24), (Punto 42 24))
    $dc.DrawLine($penLinea, (Punto 16 31), (Punto 42 31))
    # Euro en badge
    $dc.DrawEllipse($azulOscuro, $null, (Punto 45 45), 13, 13)
    Texto $dc ([char]0x20AC) $blanco 18 45 44
}

# Cuadre facturas: la balanza
New-Icon 'CanalesExternosCuadre.png' {
    param($dc)
    $penBarra = New-Pen $azul 4 -Redondo
    $dc.DrawLine($penBarra, (Punto 32 10), (Punto 32 50))
    $dc.DrawLine($penBarra, (Punto 12 16), (Punto 52 16))
    # Platillos con sus cuerdas
    $penCuerda = New-Pen $azul 2 -Redondo
    $dc.DrawLine($penCuerda, (Punto 12 16), (Punto 4 28))
    $dc.DrawLine($penCuerda, (Punto 12 16), (Punto 20 28))
    $dc.DrawLine($penCuerda, (Punto 52 16), (Punto 44 28))
    $dc.DrawLine($penCuerda, (Punto 52 16), (Punto 60 28))
    $dc.DrawGeometry($azulOscuro, $null, (Geo 'M 2 28 A 10 10 0 0 0 22 28 Z'))
    $dc.DrawGeometry($azulOscuro, $null, (Geo 'M 42 28 A 10 10 0 0 0 62 28 Z'))
    # Base
    $dc.DrawRectangle($azulOscuro, $null, (Rect 20 50 24 6))
}

# ============================================================================
# TANDA 4 - Modulos/Cajas (los 2 que van prestados de Cajas.png)
# ============================================================================

# Bancos: el edificio de columnas
New-Icon 'Bancos.png' {
    param($dc)
    $dc.DrawGeometry($azulOscuro, $null, (Geo 'M 32 4 L 58 20 L 6 20 Z'))
    foreach ($x in @(10, 22, 34, 46)) {
        $dc.DrawRectangle($azul, $null, (Rect $x 24 8 24))
    }
    $dc.DrawRectangle($azulOscuro, $null, (Rect 8 50 48 4))
    $dc.DrawRectangle($azulOscuro, $null, (Rect 5 56 54 5))
}

# Mayor Cuenta: el libro mayor abierto
New-Icon 'MayorCuenta.png' {
    param($dc)
    $penLibro = New-Pen $azul 2.5
    $dc.DrawGeometry($blanco, $penLibro, (Geo 'M 32 16 C 24 10 13 9 6 13 L 6 50 C 13 46 24 47 32 53 Z'))
    $dc.DrawGeometry($blanco, $penLibro, (Geo 'M 32 16 C 40 10 51 9 58 13 L 58 50 C 51 46 40 47 32 53 Z'))
    $penLinea = New-Pen $gris 2 -Redondo
    foreach ($y in @(22, 29, 36)) {
        $dc.DrawLine($penLinea, (Punto 12 $y), (Punto 26 ($y + 1)))
        $dc.DrawLine($penLinea, (Punto 38 ($y + 1)), (Punto 52 $y))
    }
    Texto $dc ([char]0x20AC) $azul 13 45 44
}

# ============================================================================
# TANDA 5 - Producto (el que va prestado de FichaProducto.png)
# ============================================================================

# Diarios: la libreta de anotar
New-Icon 'Diarios.png' {
    param($dc)
    $dc.DrawRoundedRectangle($azul, $null, (Rect 14 6 36 52), 3, 3)
    # Anillas
    foreach ($y in @(13, 22, 31, 40, 49)) {
        $dc.DrawEllipse($blanco, $null, (Punto 14 $y), 2.5, 2.5)
    }
    # Lineas de la pagina
    $penLinea = New-Pen $blanco 2.5 -Redondo
    foreach ($y in @(17, 25, 33, 41)) {
        $dc.DrawLine($penLinea, (Punto 23 $y), (Punto 44 $y))
    }
}

Write-Host 'Todos generados'
