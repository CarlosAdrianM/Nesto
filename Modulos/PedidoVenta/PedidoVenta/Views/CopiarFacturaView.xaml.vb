Public Class CopiarFacturaView

    ''' <summary>Lo que ocupan el título y el marco de la ventana, mientras no se pueda medir.</summary>
    Public Const MARCO_Y_TITULO As Double = 60

    Private ventana As Window

    Public Sub New()
        InitializeComponent()
        MaxHeight = AltoMaximo(SystemParameters.WorkArea.Height, MinHeight, MARCO_Y_TITULO)
        AddHandler Loaded, AddressOf AlCargar
        AddHandler Unloaded, AddressOf AlDescargar
    End Sub

    ' Incidencia 435 (30/09/26): la ventana crece hacia abajo cuando aparecen las facturas del
    ' cliente o el resultado, y el pie con «Ejecutar» se metía debajo de la barra de tareas. Con
    ' la ventana ya abierta se mide su marco de verdad y se la mantiene centrada en la pantalla.
    Private Sub AlCargar(sender As Object, e As RoutedEventArgs)
        ventana = Window.GetWindow(Me)
        If ventana Is Nothing Then
            Return
        End If
        Dim marco As Double = ventana.ActualHeight - ActualHeight
        If marco > 0 Then
            MaxHeight = AltoMaximo(SystemParameters.WorkArea.Height, MinHeight, marco)
        End If
        AddHandler ventana.SizeChanged, AddressOf AlCambiarDeTamano
        Centrar()
    End Sub

    Private Sub AlDescargar(sender As Object, e As RoutedEventArgs)
        If ventana IsNot Nothing Then
            RemoveHandler ventana.SizeChanged, AddressOf AlCambiarDeTamano
            ventana = Nothing
        End If
    End Sub

    Private Sub AlCambiarDeTamano(sender As Object, e As SizeChangedEventArgs)
        Centrar()
    End Sub

    Private Sub Centrar()
        If ventana Is Nothing OrElse ventana.WindowState <> WindowState.Normal Then
            Return
        End If
        Dim area As Rect = SystemParameters.WorkArea
        ' Solo si la ventana está en la pantalla principal: de las demás no se sabe el área útil
        If ventana.Left + ventana.ActualWidth / 2 < area.Left OrElse ventana.Left + ventana.ActualWidth / 2 > area.Right Then
            Return
        End If
        ventana.Top = ArribaCentrado(area.Top, area.Height, ventana.ActualHeight)
    End Sub

    ''' <summary>
    ''' Incidencia 435: el contenido no puede ser más alto que la pantalla menos el marco y el
    ''' título de la ventana, o lo de abajo —cliente destino y «Ejecutar»— queda inalcanzable.
    ''' </summary>
    Public Shared Function AltoMaximo(altoAreaDeTrabajo As Double, altoMinimo As Double, Optional marcoYTitulo As Double = MARCO_Y_TITULO) As Double
        Return Math.Max(altoMinimo, altoAreaDeTrabajo - marcoYTitulo)
    End Function

    ''' <summary>
    ''' Dónde va el borde de arriba para que la ventana quede centrada en el área útil (sin la
    ''' barra de tareas). Si no cabe, arriba del todo: que lo que se corte sea el final, que se
    ''' alcanza con la barra de desplazamiento.
    ''' </summary>
    Public Shared Function ArribaCentrado(arribaDelArea As Double, altoDelArea As Double, altoDeLaVentana As Double) As Double
        Return arribaDelArea + Math.Max(0, (altoDelArea - altoDeLaVentana) / 2)
    End Function

End Class
