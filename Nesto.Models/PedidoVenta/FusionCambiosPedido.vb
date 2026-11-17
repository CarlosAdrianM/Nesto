Imports System.Collections.ObjectModel
Imports System.Globalization
Imports Newtonsoft.Json

''' <summary>Nesto#642 (ampliación): qué tipo de choque hay entre lo que ha cambiado el usuario y lo que ha guardado otro.</summary>
Public Enum TipoConflictoPedido
    ''' <summary>Los dos han cambiado el mismo campo de la cabecera a valores distintos.</summary>
    CampoCabecera
    ''' <summary>Los dos han cambiado el mismo campo de la misma línea a valores distintos.</summary>
    CampoLinea
    ''' <summary>El usuario ha cambiado una línea que el otro ha borrado.</summary>
    LineaBorradaPorElOtro
    ''' <summary>El usuario ha borrado una línea que el otro ha cambiado.</summary>
    LineaBorradaPorUsted
    ''' <summary>Los dos han cambiado el importe del mismo prepago.</summary>
    Prepago
End Enum

''' <summary>Nesto#642: un choque de la fusión, con lo que hay que preguntar. Por defecto se queda lo del otro (lo guardado).</summary>
Public Class ConflictoPedido
    Public Property Tipo As TipoConflictoPedido
    ''' <summary>«la cantidad», «la forma de pago».</summary>
    Public Property Etiqueta As String
    ''' <summary>Nº de orden de la línea (0 en la cabecera).</summary>
    Public Property NumeroLinea As Integer
    Public Property Producto As String
    Public Property ValorUsted As String
    Public Property ValorOtro As String
    Friend Property AplicarLoDeUsted As Action

    ''' <summary>
    ''' La pregunta, de usted: «Alfredo cambió la cantidad de la línea 3 (38093) a 5 y usted a 8: ¿con cuál se queda?».
    ''' </summary>
    Public Function Pregunta(otro As String) As String
        Dim quien As String = If(String.IsNullOrWhiteSpace(otro), "Otra persona", otro.Trim())
        Select Case Tipo
            Case TipoConflictoPedido.LineaBorradaPorElOtro
                Return $"{quien} borró {TextoLinea()} y usted la cambió: ¿la vuelve a añadir con sus cambios?"
            Case TipoConflictoPedido.LineaBorradaPorUsted
                Return $"{quien} cambió {TextoLinea()} y usted la borró: ¿la borra igualmente?"
            Case TipoConflictoPedido.CampoLinea
                Return $"{quien} cambió {Etiqueta} de {TextoLinea()} a {ValorOtro} y usted a {ValorUsted}: ¿con cuál se queda?"
            Case Else
                Return $"{quien} cambió {Etiqueta} a {ValorOtro} y usted a {ValorUsted}: ¿con cuál se queda?"
        End Select
    End Function

    ''' <summary>El botón de quedarse con lo del usuario.</summary>
    Public ReadOnly Property TextoOpcionUsted As String
        Get
            Select Case Tipo
                Case TipoConflictoPedido.LineaBorradaPorElOtro
                    Return "Volver a añadirla"
                Case TipoConflictoPedido.LineaBorradaPorUsted
                    Return "Borrarla"
                Case Else
                    Return $"Con {ValorUsted} (el mío)"
            End Select
        End Get
    End Property

    ''' <summary>El botón de quedarse con lo del otro (lo guardado).</summary>
    Public ReadOnly Property TextoOpcionOtro As String
        Get
            Select Case Tipo
                Case TipoConflictoPedido.LineaBorradaPorElOtro
                    Return "Dejarla borrada"
                Case TipoConflictoPedido.LineaBorradaPorUsted
                    Return "Dejarla"
                Case Else
                    Return $"Con {ValorOtro}"
            End Select
        End Get
    End Property

    Private Function TextoLinea() As String
        Return If(String.IsNullOrWhiteSpace(Producto), $"la línea {NumeroLinea}", $"la línea {NumeroLinea} ({Producto.Trim()})")
    End Function
End Class

''' <summary>Nesto#642: el pedido del servidor con los cambios del usuario encima, y lo que queda por preguntar.</summary>
Public Class ResultadoFusionPedido
    ''' <summary>El pedido recién leído con los cambios del usuario que no chocan (y, al resolver, los que sí).</summary>
    Public Property Pedido As PedidoVentaDTO
    Public Property Conflictos As New List(Of ConflictoPedido)
    ''' <summary>Cuántos cambios del usuario se han puesto sin preguntar.</summary>
    Public Property CambiosAplicados As Integer
    ''' <summary>Lo que no se ha podido conservar (p. ej. los vencimientos a mano si el otro también los cambió).</summary>
    Public Property Avisos As New List(Of String)

    ''' <summary>Decide un choque: True = lo del usuario; False = lo del otro (no se toca nada).</summary>
    Public Sub Resolver(conflicto As ConflictoPedido, quedarseConLoDeUsted As Boolean)
        If conflicto IsNot Nothing AndAlso quedarseConLoDeUsted Then
            conflicto.AplicarLoDeUsted?.Invoke()
        End If
    End Sub
End Class

''' <summary>
''' Nesto#642 (ampliación, OK de Carlos 17/11/26): «Recargar conservando mis cambios». LA REGLA, pura:
''' - El cambio del usuario (delta) = la foto del pedido al cargarlo frente a lo que tiene en pantalla: líneas nuevas (sin Nº de
'''   orden), líneas cambiadas (por Nº de orden, campo a campo), líneas borradas, campos de cabecera cambiados y prepagos.
''' - Se aplica encima del pedido recién leído. Solo hay choque si los dos han tocado lo mismo: el mismo campo de la misma línea, una
'''   línea que uno borró y el otro cambió, o el mismo campo de cabecera, y a valores distintos (si los dos pusieron lo mismo, no
'''   choca). Las líneas nuevas nunca chocan.
''' - Los campos que dependen del producto (IVA, recargo, descuento del producto, ficticio) van con el producto: una sola pregunta.
''' </summary>
Public Module FusionCambiosPedido

    Private NotInheritable Class Campo(Of T)
        Public Sub New(etiqueta As String, leer As Func(Of T, Object()), copiar As Action(Of T, T), Optional formato As Func(Of Object, String) = Nothing)
            Me.Etiqueta = etiqueta
            Me.Leer = leer
            Me.Copiar = copiar
            Me.Formato = formato
        End Sub
        Public ReadOnly Property Etiqueta As String
        ''' <summary>Los valores que se comparan (el primero es el que se enseña).</summary>
        Public ReadOnly Property Leer As Func(Of T, Object())
        ''' <summary>Copia el campo de (origen) a (destino).</summary>
        Public ReadOnly Property Copiar As Action(Of T, T)
        Public ReadOnly Property Formato As Func(Of Object, String)
    End Class

    Private ReadOnly Porcentaje As Func(Of Object, String) =
        Function(v) If(TypeOf v Is Decimal, (CDec(v) * 100D).ToString("0.##", CultureInfo.GetCultureInfo("es-ES")) & " %", Mostrar(v))

    Private ReadOnly CAMPOS_CABECERA As New List(Of Campo(Of PedidoVentaDTO)) From {
        New Campo(Of PedidoVentaDTO)("la forma de pago", Function(p) {p.formaPago}, Sub(o, d) d.formaPago = o.formaPago),
        New Campo(Of PedidoVentaDTO)("los plazos de pago", Function(p) {p.plazosPago}, Sub(o, d) d.plazosPago = o.plazosPago),
        New Campo(Of PedidoVentaDTO)("el primer vencimiento", Function(p) {p.primerVencimiento}, Sub(o, d) d.primerVencimiento = o.primerVencimiento),
        New Campo(Of PedidoVentaDTO)("la cuenta de cobro (CCC)", Function(p) {p.ccc}, Sub(o, d) d.ccc = o.ccc),
        New Campo(Of PedidoVentaDTO)("el IVA", Function(p) {p.iva}, Sub(o, d) d.iva = o.iva),
        New Campo(Of PedidoVentaDTO)("el vendedor", Function(p) {p.vendedor}, Sub(o, d) d.vendedor = o.vendedor),
        New Campo(Of PedidoVentaDTO)("el periodo de facturación", Function(p) {p.periodoFacturacion}, Sub(o, d) d.periodoFacturacion = o.periodoFacturacion),
        New Campo(Of PedidoVentaDTO)("la ruta", Function(p) {p.ruta}, Sub(o, d) d.ruta = o.ruta),
        New Campo(Of PedidoVentaDTO)("la serie", Function(p) {p.serie}, Sub(o, d) d.serie = o.serie),
        New Campo(Of PedidoVentaDTO)("el contacto de entrega", Function(p) {p.contacto}, Sub(o, d) d.contacto = o.contacto),
        New Campo(Of PedidoVentaDTO)("el contacto de cobro", Function(p) {p.contactoCobro}, Sub(o, d) d.contactoCobro = o.contactoCobro),
        New Campo(Of PedidoVentaDTO)("los comentarios", Function(p) {p.comentarios}, Sub(o, d) d.comentarios = o.comentarios),
        New Campo(Of PedidoVentaDTO)("el comentario de picking", Function(p) {p.comentarioPicking}, Sub(o, d) d.comentarioPicking = o.comentarioPicking),
        New Campo(Of PedidoVentaDTO)("su pedido (P.O.)", Function(p) {p.suPedido}, Sub(o, d) d.suPedido = o.suPedido),
        New Campo(Of PedidoVentaDTO)("«no comisiona»", Function(p) {p.noComisiona}, Sub(o, d) d.noComisiona = o.noComisiona),
        New Campo(Of PedidoVentaDTO)("«mantener junto»", Function(p) {p.mantenerJunto}, Sub(o, d) d.mantenerJunto = o.mantenerJunto),
        New Campo(Of PedidoVentaDTO)("«servir junto»", Function(p) {p.servirJunto}, Sub(o, d) d.servirJunto = o.servirJunto),
        New Campo(Of PedidoVentaDTO)("el modo de entrega", Function(p) {p.modoServicio}, Sub(o, d) d.modoServicio = o.modoServicio),
        New Campo(Of PedidoVentaDTO)("el modo de facturación", Function(p) {p.modoFacturacion}, Sub(o, d) d.modoFacturacion = o.modoFacturacion),
        New Campo(Of PedidoVentaDTO)("«avisar con importe al coger el picking»", Function(p) {p.avisarConImporteAlCogerPicking}, Sub(o, d) d.avisarConImporteAlCogerPicking = o.avisarConImporteAlCogerPicking),
        New Campo(Of PedidoVentaDTO)("«nota de entrega»", Function(p) {p.notaEntrega}, Sub(o, d) d.notaEntrega = o.notaEntrega),
        New Campo(Of PedidoVentaDTO)("«recoge en tienda»", Function(p) {p.RecogeEnTienda},
                                     Sub(o, d)
                                         d.RecogeEnTienda = o.RecogeEnTienda
                                         d.RecogeEnTiendaTocado = True
                                     End Sub)
    }

    Private ReadOnly CAMPOS_LINEA As New List(Of Campo(Of LineaPedidoVentaDTO)) From {
        New Campo(Of LineaPedidoVentaDTO)("el producto",
            Function(l) {l.Producto, l.iva, l.PorcentajeIva, l.PorcentajeRecargoEquivalencia, l.DescuentoProducto, l.EsFicticio},
            Sub(o, d)
                d.Producto = o.Producto
                d.iva = o.iva
                d.PorcentajeIva = o.PorcentajeIva
                d.PorcentajeRecargoEquivalencia = o.PorcentajeRecargoEquivalencia
                d.DescuentoProducto = o.DescuentoProducto
                d.EsFicticio = o.EsFicticio
            End Sub),
        New Campo(Of LineaPedidoVentaDTO)("el texto", Function(l) {l.texto}, Sub(o, d) d.texto = o.texto),
        New Campo(Of LineaPedidoVentaDTO)("la cantidad", Function(l) {l.Cantidad}, Sub(o, d) d.Cantidad = o.Cantidad),
        New Campo(Of LineaPedidoVentaDTO)("el precio", Function(l) {l.PrecioUnitario}, Sub(o, d) d.PrecioUnitario = o.PrecioUnitario),
        New Campo(Of LineaPedidoVentaDTO)("el descuento", Function(l) {l.DescuentoLinea}, Sub(o, d) d.DescuentoLinea = o.DescuentoLinea, Porcentaje),
        New Campo(Of LineaPedidoVentaDTO)("«aplicar descuento»", Function(l) {l.AplicarDescuento}, Sub(o, d) d.AplicarDescuento = o.AplicarDescuento),
        New Campo(Of LineaPedidoVentaDTO)("la fecha de entrega", Function(l) {l.fechaEntrega}, Sub(o, d) d.fechaEntrega = o.fechaEntrega),
        New Campo(Of LineaPedidoVentaDTO)("el almacén", Function(l) {l.almacen}, Sub(o, d) d.almacen = o.almacen),
        New Campo(Of LineaPedidoVentaDTO)("la forma de venta", Function(l) {l.formaVenta}, Sub(o, d) d.formaVenta = o.formaVenta),
        New Campo(Of LineaPedidoVentaDTO)("la delegación", Function(l) {l.delegacion}, Sub(o, d) d.delegacion = o.delegacion),
        New Campo(Of LineaPedidoVentaDTO)("el tipo de línea", Function(l) {l.tipoLinea}, Sub(o, d) d.tipoLinea = o.tipoLinea),
        New Campo(Of LineaPedidoVentaDTO)("el grupo", Function(l) {l.GrupoProducto}, Sub(o, d) d.GrupoProducto = o.GrupoProducto),
        New Campo(Of LineaPedidoVentaDTO)("la oferta", Function(l) {l.oferta}, Sub(o, d) d.oferta = o.oferta),
        New Campo(Of LineaPedidoVentaDTO)("el visto bueno", Function(l) {l.vistoBueno}, Sub(o, d) d.vistoBueno = o.vistoBueno),
        New Campo(Of LineaPedidoVentaDTO)("el evento", Function(l) {l.EventoId, l.ModalidadEvento},
            Sub(o, d)
                d.EventoId = o.EventoId
                d.ModalidadEvento = o.ModalidadEvento
            End Sub)
    }

    ''' <summary>Una copia independiente (por JSON, como viaja a la API).</summary>
    Public Function ClonarPedido(pedido As PedidoVentaDTO) As PedidoVentaDTO
        If pedido Is Nothing Then
            Return Nothing
        End If
        Dim ajustes As New JsonSerializerSettings With {.ReferenceLoopHandling = ReferenceLoopHandling.Ignore}
        Return JsonConvert.DeserializeObject(Of PedidoVentaDTO)(JsonConvert.SerializeObject(pedido, ajustes), ajustes)
    End Function

    ''' <summary>
    ''' Pone encima de <paramref name="servidor"/> (lo recién leído; se MODIFICA) lo que el usuario ha cambiado de <paramref name="foto"/>
    ''' (el pedido al cargarlo) a <paramref name="mio"/> (lo que tiene en pantalla). Los choques no se aplican: van en Conflictos.
    ''' </summary>
    Public Function Fusionar(foto As PedidoVentaDTO, mio As PedidoVentaDTO, servidor As PedidoVentaDTO) As ResultadoFusionPedido
        If foto Is Nothing Then Throw New ArgumentNullException(NameOf(foto))
        If mio Is Nothing Then Throw New ArgumentNullException(NameOf(mio))
        If servidor Is Nothing Then Throw New ArgumentNullException(NameOf(servidor))
        Dim resultado As New ResultadoFusionPedido With {.Pedido = servidor}
        FusionarCabecera(foto, mio, servidor, resultado)
        FusionarLineas(foto, mio, servidor, resultado)
        FusionarPrepagos(foto, mio, servidor, resultado)
        FusionarEfectos(foto, mio, servidor, resultado)
        Return resultado
    End Function

    Private Sub FusionarCabecera(foto As PedidoVentaDTO, mio As PedidoVentaDTO, servidor As PedidoVentaDTO, resultado As ResultadoFusionPedido)
        For Each campo In CAMPOS_CABECERA
            Dim c = campo
            Dim enFoto = c.Leer.Invoke(foto), enMio = c.Leer.Invoke(mio), enServidor = c.Leer.Invoke(servidor)
            If Iguales(enMio, enFoto) Then
                Continue For ' el usuario no lo ha tocado: se queda lo del servidor
            End If
            If Iguales(enServidor, enFoto) OrElse Iguales(enServidor, enMio) Then
                c.Copiar.Invoke(mio, servidor)
                resultado.CambiosAplicados += 1
            Else
                resultado.Conflictos.Add(New ConflictoPedido With {
                    .Tipo = TipoConflictoPedido.CampoCabecera, .Etiqueta = c.Etiqueta,
                    .ValorUsted = Mostrar(enMio(0), c.Formato), .ValorOtro = Mostrar(enServidor(0), c.Formato),
                    .AplicarLoDeUsted = Sub() c.Copiar.Invoke(mio, servidor)})
            End If
        Next
    End Sub

    ''' <summary>Las líneas en blanco (producto sin referencia) no cuentan: se quitan al guardar.</summary>
    Private Function EsLineaEnBlanco(l As LineaPedidoVentaDTO) As Boolean
        Return l.tipoLinea.HasValue AndAlso l.tipoLinea.Value = 1 AndAlso String.IsNullOrWhiteSpace(l.Producto)
    End Function

    Private Sub FusionarLineas(foto As PedidoVentaDTO, mio As PedidoVentaDTO, servidor As PedidoVentaDTO, resultado As ResultadoFusionPedido)
        If servidor.Lineas Is Nothing Then
            servidor.Lineas = New List(Of LineaPedidoVentaDTO)
        End If
        Dim lineasFoto = If(foto.Lineas, New List(Of LineaPedidoVentaDTO)).Where(Function(l) l IsNot Nothing AndAlso l.id > 0).
            GroupBy(Function(l) l.id).ToDictionary(Function(g) g.Key, Function(g) g.First())
        Dim lineasMias = If(mio.Lineas, New List(Of LineaPedidoVentaDTO)).Where(Function(l) l IsNot Nothing).ToList()

        ' 1. Las nuevas (sin Nº de orden): nunca chocan
        For Each nueva In lineasMias.Where(Function(l) l.id <= 0 AndAlso Not EsLineaEnBlanco(l))
            AnadirCopia(nueva, servidor)
            resultado.CambiosAplicados += 1
        Next

        ' 2. Las que había al cargar: cambiadas o borradas por el usuario
        For Each par In lineasFoto
            Dim enFoto As LineaPedidoVentaDTO = par.Value
            Dim enMio As LineaPedidoVentaDTO = lineasMias.FirstOrDefault(Function(l) l.id = par.Key)
            Dim enServidor As LineaPedidoVentaDTO = servidor.Lineas.FirstOrDefault(Function(l) l IsNot Nothing AndAlso l.id = par.Key)

            If enMio Is Nothing Then
                ' La ha borrado el usuario
                If enServidor Is Nothing Then
                    Continue For ' el otro también
                End If
                If CamposCambiados(enFoto, enServidor).Count = 0 Then
                    servidor.Lineas.Remove(enServidor)
                    resultado.CambiosAplicados += 1
                Else
                    Dim aBorrar = enServidor
                    resultado.Conflictos.Add(New ConflictoPedido With {
                        .Tipo = TipoConflictoPedido.LineaBorradaPorUsted, .NumeroLinea = par.Key, .Producto = enFoto.Producto,
                        .AplicarLoDeUsted = Sub() servidor.Lineas.Remove(aBorrar)})
                End If
                Continue For
            End If

            Dim cambiados = CamposCambiados(enFoto, enMio)
            If cambiados.Count = 0 Then
                Continue For
            End If
            If enServidor Is Nothing Then
                ' La ha borrado el otro y el usuario la ha cambiado
                Dim mia = enMio
                resultado.Conflictos.Add(New ConflictoPedido With {
                    .Tipo = TipoConflictoPedido.LineaBorradaPorElOtro, .NumeroLinea = par.Key, .Producto = enFoto.Producto,
                    .AplicarLoDeUsted = Sub() AnadirCopia(mia, servidor)})
                Continue For
            End If
            For Each campo In cambiados
                Dim c = campo, mia = enMio, suya = enServidor
                Dim vFoto = c.Leer.Invoke(enFoto), vMio = c.Leer.Invoke(enMio), vServidor = c.Leer.Invoke(enServidor)
                If Iguales(vServidor, vFoto) OrElse Iguales(vServidor, vMio) Then
                    c.Copiar.Invoke(enMio, enServidor)
                    resultado.CambiosAplicados += 1
                Else
                    resultado.Conflictos.Add(New ConflictoPedido With {
                        .Tipo = TipoConflictoPedido.CampoLinea, .Etiqueta = c.Etiqueta, .NumeroLinea = par.Key, .Producto = enFoto.Producto,
                        .ValorUsted = Mostrar(vMio(0), c.Formato), .ValorOtro = Mostrar(vServidor(0), c.Formato),
                        .AplicarLoDeUsted = Sub() c.Copiar.Invoke(mia, suya)})
                End If
            Next
        Next
    End Sub

    Private Function CamposCambiados(antes As LineaPedidoVentaDTO, despues As LineaPedidoVentaDTO) As List(Of Campo(Of LineaPedidoVentaDTO))
        Return CAMPOS_LINEA.Where(Function(c) Not Iguales(c.Leer.Invoke(antes), c.Leer.Invoke(despues))).ToList()
    End Function

    ''' <summary>Una copia de la línea del usuario como línea nueva (sin Nº de orden) al final del pedido.</summary>
    Private Sub AnadirCopia(linea As LineaPedidoVentaDTO, pedido As PedidoVentaDTO)
        Dim copia As LineaPedidoVentaDTO = linea.ShallowCopy()
        copia.id = 0
        copia.Pedido = pedido
        pedido.Lineas.Add(copia)
    End Sub

    Private Sub FusionarPrepagos(foto As PedidoVentaDTO, mio As PedidoVentaDTO, servidor As PedidoVentaDTO, resultado As ResultadoFusionPedido)
        If servidor.Prepagos Is Nothing Then
            servidor.Prepagos = New ObservableCollection(Of PrepagoDTO)
        End If
        Dim clave As Func(Of PrepagoDTO, String) = Function(p) If(p.ConceptoAdicional, String.Empty).Trim()
        Dim deFoto = If(foto.Prepagos, New ObservableCollection(Of PrepagoDTO)).Where(Function(p) p IsNot Nothing AndAlso p.Factura Is Nothing).ToList()
        Dim mios = If(mio.Prepagos, New ObservableCollection(Of PrepagoDTO)).Where(Function(p) p IsNot Nothing AndAlso p.Factura Is Nothing).ToList()
        For Each nuevo In mios.Where(Function(m) Not deFoto.Any(Function(f) clave(f) = clave(m)))
            If Not servidor.Prepagos.Any(Function(s) s.Factura Is Nothing AndAlso clave(s) = clave(nuevo)) Then
                servidor.Prepagos.Add(New PrepagoDTO With {.Importe = nuevo.Importe, .CuentaContable = nuevo.CuentaContable,
                    .ConceptoAdicional = nuevo.ConceptoAdicional, .Pedido = servidor.numero})
                resultado.CambiosAplicados += 1
            End If
        Next
        For Each antes In deFoto
            Dim k As String = clave(antes)
            Dim enMio = mios.FirstOrDefault(Function(m) clave(m) = k)
            Dim enServidor = servidor.Prepagos.FirstOrDefault(Function(s) s.Factura Is Nothing AndAlso clave(s) = k)
            If enServidor Is Nothing Then
                Continue For
            End If
            If enMio Is Nothing Then
                If enServidor.Importe = antes.Importe Then
                    servidor.Prepagos.Remove(enServidor) ' lo quitó el usuario, viéndolo
                    resultado.CambiosAplicados += 1
                End If
                Continue For
            End If
            If enMio.Importe = antes.Importe Then
                Continue For
            End If
            If enServidor.Importe = antes.Importe OrElse enServidor.Importe = enMio.Importe Then
                enServidor.Importe = enMio.Importe
                resultado.CambiosAplicados += 1
            Else
                Dim suyo = enServidor, nuevoImporte = enMio.Importe
                resultado.Conflictos.Add(New ConflictoPedido With {
                    .Tipo = TipoConflictoPedido.Prepago, .Etiqueta = $"el importe del prepago «{k}»",
                    .ValorUsted = Mostrar(enMio.Importe), .ValorOtro = Mostrar(enServidor.Importe),
                    .AplicarLoDeUsted = Sub() suyo.Importe = nuevoImporte})
            End If
        Next
    End Sub

    Private Sub FusionarEfectos(foto As PedidoVentaDTO, mio As PedidoVentaDTO, servidor As PedidoVentaDTO, resultado As ResultadoFusionPedido)
        If Not mio.CrearEfectosManualmente OrElse JsonEfectos(mio) = JsonEfectos(foto) Then
            Return
        End If
        If JsonEfectos(servidor) = JsonEfectos(foto) Then
            Dim copia = ClonarPedido(mio)
            servidor.CrearEfectosManualmente = True
            servidor.Efectos = copia.Efectos
            resultado.CambiosAplicados += 1
        Else
            resultado.Avisos.Add("No se han conservado los vencimientos que puso a mano porque también los han cambiado: revíselos antes de guardar.")
        End If
    End Sub

    Private Function JsonEfectos(pedido As PedidoVentaDTO) As String
        If pedido?.Efectos Is Nothing Then
            Return "[]"
        End If
        Return JsonConvert.SerializeObject(pedido.Efectos.Select(Function(e) New With {e.FechaVencimiento, e.Importe, e.FormaPago, e.Ccc}).ToList())
    End Function

    ''' <summary>Iguales con Nothing = "" y sin los espacios de relleno de la BD.</summary>
    Friend Function Iguales(a As Object(), b As Object()) As Boolean
        If a.Length <> b.Length Then
            Return False
        End If
        For i = 0 To a.Length - 1
            If Not Iguales(a(i), b(i)) Then
                Return False
            End If
        Next
        Return True
    End Function

    Private Function Iguales(a As Object, b As Object) As Boolean
        If TypeOf a Is String OrElse TypeOf b Is String OrElse (a Is Nothing AndAlso b Is Nothing) Then
            Return String.Equals(If(TryCast(a, String), If(a?.ToString(), String.Empty)).Trim(),
                                 If(TryCast(b, String), If(b?.ToString(), String.Empty)).Trim(), StringComparison.Ordinal)
        End If
        If a Is Nothing OrElse b Is Nothing Then
            Return False
        End If
        Return a.Equals(b)
    End Function

    Private Function Mostrar(valor As Object, Optional formato As Func(Of Object, String) = Nothing) As String
        If formato IsNot Nothing Then
            Return formato(valor)
        End If
        If valor Is Nothing Then
            Return "(vacío)"
        End If
        If TypeOf valor Is Date Then
            Return CDate(valor).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
        End If
        If TypeOf valor Is Boolean Then
            Return If(CBool(valor), "sí", "no")
        End If
        If TypeOf valor Is Decimal Then
            Return CDec(valor).ToString("0.####", CultureInfo.GetCultureInfo("es-ES"))
        End If
        Dim texto As String = valor.ToString().Trim()
        Return If(texto.Length = 0, "(vacío)", texto)
    End Function
End Module
