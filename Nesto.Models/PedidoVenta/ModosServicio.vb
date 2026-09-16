''' <summary>
''' Nesto#476 / NestoAPI#482: el modo de servicio del pedido, que sustituye a la casilla «Servir junto».
''' Réplica de Constantes.Pedidos.ModosServicio de NestoAPI. El servidor normaliza en POST/PUT:
''' sin modo deriva de servirJunto; con modo, servirJunto pasa a ser su derivado (solo el 1 es True).
''' El modo 3 (tras reponer de tiendas) existe en el diseño pero el servidor lo rechaza todavía
''' (NestoAPI#482, slice 1), así que NO se ofrece en la lista.
''' </summary>
Public NotInheritable Class ModosServicio
    Private Sub New()
    End Sub

    Public Const TODO_JUNTO As Byte = 1
    Public Const SEGUN_VAYA_ENTRANDO As Byte = 2
    Public Const TRAS_REPONER_DE_TIENDAS As Byte = 3
    Public Const AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ As Byte = 4

    Private Shared ReadOnly _lista As IReadOnlyList(Of ModoServicioItem) = New List(Of ModoServicioItem) From {
        New ModoServicioItem(TODO_JUNTO, "Todo junto", "No sale nada hasta que hay stock de todo el pedido (la antigua casilla «Servir junto» marcada)."),
        New ModoServicioItem(SEGUN_VAYA_ENTRANDO, "Según vaya entrando", "Sale lo que haya en cada pasada, tantas entregas como haga falta (la antigua casilla desmarcada)."),
        New ModoServicioItem(AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ, "Ahora lo que hay, el resto de una vez", "Sale ya lo que hay; lo que falta se entrega en una sola entrega más, cuando esté todo.")
    }

    ''' <summary>Los modos que se pueden elegir en pantalla (el 3 queda fuera hasta que el servidor lo admita).</summary>
    Public Shared ReadOnly Property Lista As IReadOnlyList(Of ModoServicioItem)
        Get
            Return _lista
        End Get
    End Property

    ''' <summary>El modo que rige de verdad: el informado o, si no hay, el que dice servirJunto.</summary>
    Public Shared Function Efectivo(modoServicio As Byte?, servirJunto As Boolean) As Byte
        If modoServicio.HasValue AndAlso EsValido(modoServicio.Value) Then
            Return modoServicio.Value
        End If
        Return If(servirJunto, TODO_JUNTO, SEGUN_VAYA_ENTRANDO)
    End Function

    Public Shared Function EsValido(modo As Byte) As Boolean
        Return modo >= TODO_JUNTO AndAlso modo <= AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ
    End Function

    ''' <summary>Solo el modo 1 es «servir junto» en el sentido de la columna y de las validaciones
    ''' (NestoAPI#220/#470): cualquier otro puede servir parcialmente en la primera pasada.</summary>
    Public Shared Function EsTodoJunto(modo As Byte) As Boolean
        Return modo = TODO_JUNTO
    End Function

    Public Shared Function Nombre(modo As Byte) As String
        Dim item = _lista.FirstOrDefault(Function(m) m.Codigo = modo)
        Return If(item?.Nombre, $"Modo {modo}")
    End Function
End Class

Public Class ModoServicioItem
    Public Sub New(codigo As Byte, nombre As String, descripcion As String)
        Me.Codigo = codigo
        Me.Nombre = nombre
        Me.Descripcion = descripcion
    End Sub

    Public ReadOnly Property Codigo As Byte
    Public ReadOnly Property Nombre As String
    Public ReadOnly Property Descripcion As String

    Public Overrides Function ToString() As String
        Return Nombre
    End Function
End Class
