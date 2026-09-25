Imports CommunityToolkit.Mvvm.ComponentModel

''' <summary>
''' NestoAPI#518 / Nesto#484: una opción de los combos «Servir» y, desde Nesto#493, «Facturación» (plantilla y
''' detalle de pedido). Los modos que no tienen sentido para el pedido se muestran DESHABILITADOS (no ocultos),
''' con el motivo que redacta la API en el tooltip, para que el usuario vea que existen y por qué no los puede
''' elegir. Antes se llamaba OpcionModoServicio; el nombre cambió al compartirla con los modos de facturación.
''' </summary>
Public Class OpcionModo
    Inherits ObservableObject

    Public Sub New(item As ModoItem)
        Codigo = item.Codigo
        Nombre = item.Nombre
        Descripcion = item.Descripcion
    End Sub

    Public ReadOnly Property Codigo As Byte
    Public ReadOnly Property Nombre As String
    Public ReadOnly Property Descripcion As String

    Private _habilitado As Boolean = True
    Public Property Habilitado As Boolean
        Get
            Return _habilitado
        End Get
        Set(value As Boolean)
            If SetProperty(_habilitado, value) Then
                OnPropertyChanged(NameOf(Ayuda))
            End If
        End Set
    End Property

    Private _motivoNoPermitido As String
    ''' <summary>Por qué no se puede elegir (texto de la API). Nothing si se puede.</summary>
    Public Property MotivoNoPermitido As String
        Get
            Return _motivoNoPermitido
        End Get
        Set(value As String)
            If SetProperty(_motivoNoPermitido, value) Then
                OnPropertyChanged(NameOf(Ayuda))
            End If
        End Set
    End Property

    ''' <summary>Tooltip del ítem: el motivo si está deshabilitado; si no, qué hace el modo.</summary>
    Public ReadOnly Property Ayuda As String
        Get
            Return If(Habilitado OrElse String.IsNullOrWhiteSpace(MotivoNoPermitido), Descripcion, MotivoNoPermitido)
        End Get
    End Property

    Public Overrides Function ToString() As String
        Return Nombre
    End Function
End Class
