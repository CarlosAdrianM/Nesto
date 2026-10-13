Imports CommunityToolkit.Mvvm.ComponentModel

''' <summary>
''' Sugerencia 575 (Alfredo, almacén): un pedido que va por OnTime (FW) y que seguramente saldrá por nuestra ruta (AT o 16).
''' Lo devuelve GET api/Picking/SugerenciasRuta y se enseña SOLO a almacén al sacar el picking, para cambiarle la ruta antes.
''' Marcado y RutaNueva los decide quien saca el picking; por defecto no se marca nada (nunca se cambia sola).
''' </summary>
Public Class SugerenciaRutaPicking
    Inherits ObservableObject

    Public Shared ReadOnly Property RutasPropias As IReadOnlyList(Of String) = New List(Of String) From {"AT", "16"}

    Public Property Empresa As String
    Public Property Pedido As Integer
    Public Property FechaPedido As Date?
    Public Property Cliente As String
    Public Property Contacto As String
    Public Property Nombre As String
    Public Property CodigoPostal As String
    Public Property Poblacion As String
    Public Property BaseImponible As Decimal
    Public Property RutaActual As String
    Public Property RutaPropuesta As String
    Public Property Motivos As List(Of String) = New List(Of String)

    Public ReadOnly Property MotivosTexto As String
        Get
            Return String.Join(" · ", If(Motivos, New List(Of String)))
        End Get
    End Property

    Public ReadOnly Property ClienteTexto As String
        Get
            Dim cp As String = If(String.IsNullOrWhiteSpace(CodigoPostal), String.Empty, $" ({CodigoPostal?.Trim()} {Poblacion?.Trim()})")
            Return $"{Cliente?.Trim()} {Nombre?.Trim()}{cp}".Trim()
        End Get
    End Property

    Private _marcado As Boolean
    Public Property Marcado As Boolean
        Get
            Return _marcado
        End Get
        Set(value As Boolean)
            SetProperty(_marcado, value)
        End Set
    End Property

    Private _rutaNueva As String
    ''' <summary>La ruta a la que se pasa si se marca: la propuesta, salvo que almacén elija la otra.</summary>
    Public Property RutaNueva As String
        Get
            Return If(_rutaNueva, RutaPropuesta)
        End Get
        Set(value As String)
            SetProperty(_rutaNueva, value)
        End Set
    End Property
End Class

''' <summary>Sugerencia 575: cómo ha ido el cambio de ruta de un pedido (POST api/Picking/CambiarRuta).</summary>
Public Class ResultadoCambioRutaPicking
    Public Property Empresa As String
    Public Property Pedido As Integer
    Public Property RutaNueva As String
    Public Property Cambiado As Boolean
    Public Property [Error] As String
End Class
