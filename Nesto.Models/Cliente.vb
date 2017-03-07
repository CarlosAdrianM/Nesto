Imports System.Collections.ObjectModel
Imports System.Drawing
Imports Nesto.Models.PedidoVenta

Public Class ClienteJson
    Public Property empresa() As String
    Public Property cliente() As String
    Public Property contacto() As String
    Public Property clientePrincipal() As Boolean
    Public Property nombre() As String
    Public Property direccion() As String
    Public Property poblacion() As String
    Public Property telefono() As String
    Public Property vendedor() As String
    Public Property comentarios() As String
    Public Property codigoPostal() As String
    Public Property cifNif() As String
    Public Property provincia() As String
    Public Property estado() As Integer
    Public Property iva() As String
    Public Property grupo() As String
    Public Property periodoFacturacion() As String
    Public Property ccc() As String
    Public Property ruta() As String
    Public Property copiasAlbaran() As Integer
    Public Property copiasFactura() As Integer
    Public Property comentarioRuta() As Object
    Public Property comentarioPicking() As Object
    Public Property web() As String
    Public Property albaranValorado() As Boolean
    Public Property cadena() As Object
    Public Property noComisiona() As Double
    Public Property servirJunto() As Boolean
    Public Property mantenerJunto() As Boolean

    Public Overridable Property VendedoresGrupoProducto As ObservableCollection(Of VendedorGrupoProductoDTO)

    Public ReadOnly Property rutaLogo As String
        Get
            If empresa = "1" OrElse empresa = "2" OrElse empresa = "4" OrElse empresa = "5" Then
                Return "pack://application:,,,/PlantillaVenta;component/Images/logo" + empresa + ".jpg"
            Else
                Return ""
            End If
        End Get
    End Property

    Public ReadOnly Property colorEstado As Brush
        Get
            If estado = -1 Then
                Return Brushes.Red
            ElseIf estado = 7 Then
                Return Brushes.Orange
            ElseIf estado = 0 OrElse estado = 9 Then
                Return Brushes.Green
            Else
                Return Brushes.Black
            End If
        End Get
    End Property
End Class
