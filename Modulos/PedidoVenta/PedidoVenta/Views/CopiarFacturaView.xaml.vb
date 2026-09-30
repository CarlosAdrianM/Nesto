Public Class CopiarFacturaView

    Public Sub New()
        InitializeComponent()
        MaxHeight = AltoMaximo(SystemParameters.WorkArea.Height, MinHeight)
    End Sub

    ''' <summary>
    ''' Incidencia 435: la ventana no puede ser más alta que la pantalla (menos el marco y el
    ''' título), o lo de abajo —cliente destino y «Ejecutar»— queda inalcanzable.
    ''' </summary>
    Public Shared Function AltoMaximo(altoAreaDeTrabajo As Double, altoMinimo As Double) As Double
        Const MARCO_Y_TITULO As Double = 60
        Return Math.Max(altoMinimo, altoAreaDeTrabajo - MARCO_Y_TITULO)
    End Function

End Class
