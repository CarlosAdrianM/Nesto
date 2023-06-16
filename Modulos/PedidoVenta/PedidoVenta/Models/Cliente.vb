Imports System.Collections.ObjectModel
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Models

Public Class ClienteJson
    Implements IFiltrableItem
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
    Public ReadOnly Property poblacionConCodigoPostal As String
        Get
            Return String.Format("{0} {1} ({2})", codigoPostal?.Trim(), poblacion?.Trim(), provincia?.Trim())
        End Get
    End Property
    Public Property usuario As String

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
                Return Brushes.DarkRed
            ElseIf estado = 5 Then
                Return Brushes.Red
            ElseIf estado = 7 Then
                Return Brushes.GreenYellow
            ElseIf estado = 0 OrElse estado = 9 Then
                Return Brushes.Green
            Else
                Return Brushes.Transparent
            End If
        End Get
    End Property

    Public Overrides Function ToString() As String
        Dim cadenaCliente As String = cliente + "/" + contacto + vbCrLf + nombre + vbCrLf + direccion + vbCr + codigoPostal + " " + poblacion + " (" + provincia + ")" + vbCr + telefono
        If Not String.IsNullOrWhiteSpace(vendedor) Then
            cadenaCliente += vbCr + "Vendedor estética: " + vendedor
        End If

        If Not IsNothing(VendedoresGrupoProducto) AndAlso Not IsNothing(VendedoresGrupoProducto.FirstOrDefault) Then
            cadenaCliente += vbCr + "Vendedor peluquería: " + VendedoresGrupoProducto.FirstOrDefault.vendedor
        End If
        Return cadenaCliente
    End Function

    Public Function Contains(filtro As String) As Boolean Implements IFiltrableItem.Contains
        Return (Not IsNothing(cliente) AndAlso cliente.ToLower.Trim = filtro.ToLower) OrElse
                (Not IsNothing(direccion) AndAlso direccion.ToLower.Contains(filtro.ToLower)) OrElse
                (Not IsNothing(nombre) AndAlso nombre.ToLower.Contains(filtro.ToLower)) OrElse
                (Not IsNothing(telefono) AndAlso telefono.ToLower.Contains(filtro.ToLower)) OrElse
                (Not IsNothing(cifNif) AndAlso cifNif.ToLower.Contains(filtro.ToLower)) OrElse
                (Not IsNothing(poblacion) AndAlso poblacion.ToLower.Contains(filtro.ToLower)) OrElse
                (Not IsNothing(comentarios) AndAlso comentarios.ToLower.Contains(filtro.ToLower))
    End Function
End Class
