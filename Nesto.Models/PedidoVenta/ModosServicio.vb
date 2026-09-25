''' <summary>
''' Nesto#476 / NestoAPI#482: el modo de servicio del pedido, que sustituye a la casilla «Servir junto».
''' Réplica de Constantes.Pedidos.ModosServicio de NestoAPI. El servidor normaliza en POST/PUT:
''' sin modo deriva de servirJunto; con modo, servirJunto pasa a ser su derivado (solo el 1 es True).
''' El modo 3 (tras reponer de tiendas) lo admite el servidor desde el slice 2 (16/09/26).
''' </summary>
Public NotInheritable Class ModosServicio
    Private Sub New()
    End Sub

    Public Const TODO_JUNTO As Byte = 1
    Public Const SEGUN_VAYA_ENTRANDO As Byte = 2
    Public Const TRAS_REPONER_DE_TIENDAS As Byte = 3
    Public Const AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ As Byte = 4

    ''' <summary>
    ''' Modo con el que nace un pedido si nadie dice otra cosa (Carlos, 16/09/26): «tras reponer de
    ''' tiendas». NO se arrastra el ServirJunto de la ficha del cliente (una referencia agotada o anulada
    ''' dejaba pedidos «todo junto» sin servir nunca). El parámetro ModoServicioPorDefecto permite excepciones.
    ''' </summary>
    Public Const POR_DEFECTO As Byte = TRAS_REPONER_DE_TIENDAS

    ''' <summary>
    ''' El valor del parámetro ModoServicioPorDefecto, o POR_DEFECTO si falta o no es un modo válido.
    ''' NestoAPI#506 cambió el contrato del parámetro: '0' (el valor que dejó el script a todos los
    ''' usuarios) significa «que lo decida el stock», y 1..4 fuerzan un modo. Aquí '0' cae en POR_DEFECTO
    ''' a propósito: es solo el respaldo mientras el servidor no ha contestado a ModoServicioSugerido
    ''' (Nesto#483). Quien manda cuando hay respuesta es el servidor, no este parámetro.
    ''' </summary>
    Public Shared Function ParsearPorDefecto(valorParametro As String) As Byte
        Dim modo As Byte
        If Byte.TryParse(If(valorParametro, String.Empty).Trim(), modo) AndAlso EsValido(modo) Then
            Return modo
        End If
        Return POR_DEFECTO
    End Function

    Private Shared ReadOnly _lista As IReadOnlyList(Of ModoServicioItem) = New List(Of ModoServicioItem) From {
        New ModoServicioItem(TODO_JUNTO, "Todo junto", "No sale nada hasta que hay stock de todo el pedido (la antigua casilla «Servir junto» marcada)."),
        New ModoServicioItem(SEGUN_VAYA_ENTRANDO, "Según vaya entrando", "Sale lo que haya en cada pasada, tantas entregas como haga falta (la antigua casilla desmarcada)."),
        New ModoServicioItem(TRAS_REPONER_DE_TIENDAS, "Tras reponer de tiendas", "Espera a que la reposición habitual traiga de las tiendas el stock que le corresponda al pedido; cuando no queda nada que traer, sale lo que hay y el resto según vaya entrando."),
        New ModoServicioItem(AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ, "Ahora lo que hay, el resto de una vez", "Sale ya lo que hay; lo que falta se entrega en una sola entrega más, cuando esté todo.")
    }

    ''' <summary>Los modos que se pueden elegir en pantalla, en el orden del selector.</summary>
    Public Shared ReadOnly Property Lista As IReadOnlyList(Of ModoServicioItem)
        Get
            Return _lista
        End Get
    End Property

    ''' <summary>
    ''' El modo que rige de verdad (misma regla que NestoAPI, Carlos 16/09/26): servirJunto marcado SIEMPRE
    ''' es «todo junto»; desmarcado, manda el modo parcial guardado (3 o 4) o, si no hay, el 2. El bool es
    ''' la autoridad de «todo junto» porque el Nesto viejo lo escribe en la tabla sin conocer el modo y
    ''' NestoApp solo manda el bool; el modo solo refina el «no todo junto».
    ''' </summary>
    Public Shared Function Efectivo(modoServicio As Byte?, servirJunto As Boolean) As Byte
        If servirJunto Then
            Return TODO_JUNTO
        End If
        If modoServicio.HasValue AndAlso (modoServicio.Value = TRAS_REPONER_DE_TIENDAS OrElse modoServicio.Value = AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ) Then
            Return modoServicio.Value
        End If
        Return SEGUN_VAYA_ENTRANDO
    End Function

    Public Shared Function EsValido(modo As Byte) As Boolean
        Return modo >= TODO_JUNTO AndAlso modo <= AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ
    End Function

    ''' <summary>Solo el modo 1 es «servir junto» en el sentido de la columna y de las validaciones
    ''' (NestoAPI#220/#470): cualquier otro puede servir parcialmente en la primera pasada.</summary>
    Public Shared Function EsTodoJunto(modo As Byte) As Boolean
        Return modo = TODO_JUNTO
    End Function

    ''' <summary>A efectos de portes (NestoAPI#211/Nesto#365, «1 entrega → todo cuenta»): 1 y 4 acaban en
    ''' una entrega única del resto; 2 y 3 son por entrega. Misma regla que GestorPortes en la API.</summary>
    Public Shared Function EsEntregaUnica(modo As Byte) As Boolean
        Return modo = TODO_JUNTO OrElse modo = AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ
    End Function

    Public Shared Function Nombre(modo As Byte) As String
        Dim item = _lista.FirstOrDefault(Function(m) m.Codigo = modo)
        Return If(item?.Nombre, $"Modo {modo}")
    End Function
End Class

''' <summary>Nesto#493: lo común (código, nombre, descripción) vive en <see cref="ModoItem"/>, compartido con los modos de facturación.</summary>
Public Class ModoServicioItem
    Inherits ModoItem

    Public Sub New(codigo As Byte, nombre As String, descripcion As String)
        MyBase.New(codigo, nombre, descripcion)
    End Sub
End Class
