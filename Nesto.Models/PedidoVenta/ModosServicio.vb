''' <summary>
''' Nesto#476 / NestoAPI#482: el modo de servicio del pedido, que sustituye a la casilla «Servir junto».
''' Réplica de Constantes.Pedidos.ModosServicio de NestoAPI. El servidor normaliza en POST/PUT:
''' sin modo deriva de servirJunto; con modo, servirJunto pasa a ser su derivado (solo el 1 y el 6 son True).
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
    ''' NestoAPI#698: pedido retenido hasta nuevo aviso (o hasta una fecha). NO se elige en el selector: solo lo ponen y
    ''' lo quitan los botones «Retener…» y «Liberar» (POST Retener/Liberar de la API). Mientras dura, el pedido no entra
    ''' en el picking y la API rechaza cambiar el modo («libérelo antes»).
    ''' </summary>
    Public Const RETENIDO As Byte = 5
    ''' <summary>
    ''' Sugerencia 1340: «Junto con lo pendiente del cliente». Refinamiento de «todo junto» (servirJunto = True): el
    ''' pedido sale entero y además no sale hasta que pueda salir lo que el mismo cliente y contacto tiene pendiente
    ''' (notas de entrega y pedidos); entonces sale todo en un solo envío. Si ya no queda nada, sale como «Todo junto».
    ''' Se elige en el selector solo cuando la API lo ofrece (o el pedido ya lo tiene); la sugerencia nunca lo propone
    ''' y NO vale como valor del parámetro ModoServicioPorDefecto.
    ''' </summary>
    Public Const JUNTO_CON_LO_PENDIENTE As Byte = 6

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
        ' Sugerencia 1340: el 6 se elige pedido a pedido; como modo por defecto solo valen 1..4.
        If Byte.TryParse(If(valorParametro, String.Empty).Trim(), modo) AndAlso
            modo >= TODO_JUNTO AndAlso modo <= AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ Then
            Return modo
        End If
        Return POR_DEFECTO
    End Function

    Private Shared ReadOnly _lista As IReadOnlyList(Of ModoServicioItem) = New List(Of ModoServicioItem) From {
        New ModoServicioItem(TODO_JUNTO, "Todo junto", "No sale nada hasta que hay stock de todo el pedido (la antigua casilla «Servir junto» marcada)."),
        New ModoServicioItem(SEGUN_VAYA_ENTRANDO, "Según vaya entrando", "Sale lo que haya en cada pasada, tantas entregas como haga falta (la antigua casilla desmarcada)."),
        New ModoServicioItem(TRAS_REPONER_DE_TIENDAS, "Tras reponer de tiendas", "Espera a que la reposición habitual traiga de las tiendas el stock que le corresponda al pedido; cuando no queda nada que traer, sale lo que hay y el resto según vaya entrando."),
        New ModoServicioItem(AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ, "Ahora lo que hay, el resto de una vez", "Sale ya lo que hay; lo que falta se entrega en una sola entrega más, cuando esté todo."),
        New ModoServicioItem(JUNTO_CON_LO_PENDIENTE, "Junto con lo pendiente del cliente", "No sale hasta que pueda salir lo que el cliente tiene pendiente para esta dirección (notas de entrega y pedidos); entonces sale todo en un solo envío. Si ya no queda nada pendiente, sale como «Todo junto».", soloSiSeOfrece:=True)
    }

    Private Shared ReadOnly _retenido As New ModoServicioItem(RETENIDO, "Retenido", "El pedido no sale hasta que alguien lo libere (o hasta la fecha indicada al retenerlo). Se pone y se quita con «Retener…» y «Liberar».")

    ''' <summary>NestoAPI#698: el modo «Retenido», solo para enseñarlo (no está en <see cref="Lista"/> porque no se elige).</summary>
    Public Shared ReadOnly Property ItemRetenido As ModoServicioItem
        Get
            Return _retenido
        End Get
    End Property

    ''' <summary>Los modos que se pueden elegir en pantalla, en el orden del selector. El 6 (sugerencia 1340) va marcado
    ''' <see cref="ModoItem.SoloSiSeOfrece"/>: el selector solo lo enseña si la API lo ofrece o el pedido ya lo tiene.</summary>
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
        ' NestoAPI#698: la retención manda sobre todo lo demás (el servirJunto guardado es el de antes de retener).
        If modoServicio.HasValue AndAlso EsRetenido(modoServicio.Value) Then
            Return RETENIDO
        End If
        If servirJunto Then
            ' Sugerencia 1340: el 6 es «todo junto» refinado; con el bool desmarcado deja de serlo (como en la API).
            Return If(modoServicio.HasValue AndAlso modoServicio.Value = JUNTO_CON_LO_PENDIENTE, JUNTO_CON_LO_PENDIENTE, TODO_JUNTO)
        End If
        If modoServicio.HasValue AndAlso (modoServicio.Value = TRAS_REPONER_DE_TIENDAS OrElse modoServicio.Value = AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ) Then
            Return modoServicio.Value
        End If
        Return SEGUN_VAYA_ENTRANDO
    End Function

    ''' <summary>Los modos que se pueden elegir (1..4 y el 6 de la sugerencia 1340). El 5 (<see cref="RETENIDO"/>) no: ver <see cref="EsRetenido"/>.</summary>
    Public Shared Function EsValido(modo As Byte) As Boolean
        Return (modo >= TODO_JUNTO AndAlso modo <= AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ) OrElse modo = JUNTO_CON_LO_PENDIENTE
    End Function

    ''' <summary>NestoAPI#698: ¿el pedido está retenido?</summary>
    Public Shared Function EsRetenido(modo As Byte?) As Boolean
        Return modo.HasValue AndAlso modo.Value = RETENIDO
    End Function

    ''' <summary>Solo el modo 1 es «servir junto» en el sentido de la columna y de las validaciones
    ''' (NestoAPI#220/#470): cualquier otro puede servir parcialmente en la primera pasada. Sugerencia 1340: el 6 también
    ''' (sale entero, servirJunto = True).</summary>
    Public Shared Function EsTodoJunto(modo As Byte) As Boolean
        Return modo = TODO_JUNTO OrElse modo = JUNTO_CON_LO_PENDIENTE
    End Function

    ''' <summary>A efectos de portes (NestoAPI#211/Nesto#365, «1 entrega → todo cuenta»): 1 y 4 acaban en
    ''' una entrega única del resto; 2 y 3 son por entrega. Misma regla que GestorPortes en la API.</summary>
    Public Shared Function EsEntregaUnica(modo As Byte) As Boolean
        Return modo = TODO_JUNTO OrElse modo = AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ OrElse modo = JUNTO_CON_LO_PENDIENTE
    End Function

    Public Shared Function Nombre(modo As Byte) As String
        If modo = RETENIDO Then
            Return _retenido.Nombre
        End If
        Dim item = _lista.FirstOrDefault(Function(m) m.Codigo = modo)
        Return If(item?.Nombre, $"Modo {modo}")
    End Function
End Class

''' <summary>Nesto#493: lo común (código, nombre, descripción) vive en <see cref="ModoItem"/>, compartido con los modos de facturación.</summary>
Public Class ModoServicioItem
    Inherits ModoItem

    Public Sub New(codigo As Byte, nombre As String, descripcion As String, Optional soloSiSeOfrece As Boolean = False)
        MyBase.New(codigo, nombre, descripcion, soloSiSeOfrece)
    End Sub
End Class
