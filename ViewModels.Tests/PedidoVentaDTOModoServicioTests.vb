Imports Nesto.Models

''' <summary>
''' Nesto#476 / NestoAPI#482: el modo de servicio viaja en PedidoVentaDTO y cuenta como cambio.
''' </summary>
<TestClass()>
Public Class PedidoVentaDTOModoServicioTests

    <TestMethod()>
    Public Sub Equals_ModoServicioDistinto_NoSonIguales()
        Dim a As New PedidoVentaDTO() With {.empresa = "1", .numero = 1, .servirJunto = False, .modoServicio = 2}
        Dim b As New PedidoVentaDTO() With {.empresa = "1", .numero = 1, .servirJunto = False, .modoServicio = 4}

        Assert.IsFalse(a.Equals(b))
        Assert.IsTrue(a.ObtenerCamposDiferentes(b).Any(Function(d) d.StartsWith("modoServicio")))
    End Sub

    <TestMethod()>
    Public Sub Equals_ModoServicioNothingEnLosDos_SonIguales()
        Dim a As New PedidoVentaDTO() With {.empresa = "1", .numero = 1, .servirJunto = True}
        Dim b As New PedidoVentaDTO() With {.empresa = "1", .numero = 1, .servirJunto = True}

        Assert.IsTrue(a.Equals(b))
    End Sub

    <TestMethod()>
    Public Sub CrearSnapshot_ConservaElModoServicio()
        Dim pedido As New PedidoVentaDTO() With {.empresa = "1", .numero = 1, .modoServicio = 4}

        Dim snapshot = pedido.CrearSnapshot()

        Assert.AreEqual(CByte(4), snapshot.modoServicio)
        Assert.IsTrue(pedido.Equals(snapshot))
    End Sub

    <TestMethod()>
    Public Sub ModosServicio_Efectivo_ServirJuntoMarcadoEsTodoJunto_DesmarcadoMandaElModoParcial()
        Assert.AreEqual(ModosServicio.TODO_JUNTO, ModosServicio.Efectivo(Nothing, True))
        Assert.AreEqual(ModosServicio.SEGUN_VAYA_ENTRANDO, ModosServicio.Efectivo(Nothing, False))
        ' Marcado manda aunque el modo guardado sea parcial (misma regla que la API)
        Assert.AreEqual(ModosServicio.TODO_JUNTO, ModosServicio.Efectivo(4, True))
        Assert.AreEqual(CByte(4), ModosServicio.Efectivo(4, False))
        Assert.AreEqual(CByte(3), ModosServicio.Efectivo(3, False))
        ' Modo 1 desmarcado (el Nesto viejo lo desmarcó): según entre
        Assert.AreEqual(ModosServicio.SEGUN_VAYA_ENTRANDO, ModosServicio.Efectivo(1, False))
    End Sub

    <TestMethod()>
    Public Sub ModosServicio_EsEntregaUnica_SoloLosModos1Y4()
        Assert.IsTrue(ModosServicio.EsEntregaUnica(1))
        Assert.IsFalse(ModosServicio.EsEntregaUnica(2))
        Assert.IsFalse(ModosServicio.EsEntregaUnica(3))
        Assert.IsTrue(ModosServicio.EsEntregaUnica(4))
    End Sub

    <TestMethod()>
    Public Sub ModosServicio_Lista_OfreceLosCuatroModosConNombreYElSeisSoloSiSeOfrece()
        Dim codigos = ModosServicio.Lista.Select(Function(m) m.Codigo).ToList()

        ' Sugerencia 1340: el 6 está en la lista, pero marcado para que el selector solo lo enseñe si la API lo ofrece.
        CollectionAssert.AreEqual(New List(Of Byte) From {1, 2, 3, 4, 6}, codigos)
        Assert.IsTrue(ModosServicio.Lista.Where(Function(m) m.Codigo <> 6).All(Function(m) Not m.SoloSiSeOfrece))
        Assert.IsTrue(ModosServicio.Lista.Single(Function(m) m.Codigo = 6).SoloSiSeOfrece)
        Assert.IsTrue(ModosServicio.Lista.All(Function(m) Not String.IsNullOrWhiteSpace(m.Nombre) AndAlso Not String.IsNullOrWhiteSpace(m.Descripcion)))
        Assert.AreEqual("Tras reponer de tiendas", ModosServicio.Nombre(ModosServicio.TRAS_REPONER_DE_TIENDAS))
        Assert.AreEqual("Junto con lo pendiente del cliente", ModosServicio.Nombre(ModosServicio.JUNTO_CON_LO_PENDIENTE))
    End Sub

    ' ---------- Sugerencia 1340: «Junto con lo pendiente del cliente» (6) ----------

    <TestMethod()>
    Public Sub ModosServicio_Efectivo_ElSeisConServirJuntoEsSeis_SinServirJuntoDejaDeSerlo()
        ' El riesgo gordo: antes Efectivo(6, True) daba 1 y el detalle lo guardaba como «Todo junto».
        Assert.AreEqual(ModosServicio.JUNTO_CON_LO_PENDIENTE, ModosServicio.Efectivo(6, True))
        ' Como en la API: con servirJunto desmarcado un 6 deja de ser 6.
        Assert.AreEqual(ModosServicio.SEGUN_VAYA_ENTRANDO, ModosServicio.Efectivo(6, False))
        ' La retención sigue mandando.
        Assert.AreEqual(ModosServicio.RETENIDO, ModosServicio.Efectivo(5, True))
    End Sub

    <TestMethod()>
    Public Sub ModosServicio_ElSeisEsTodoJuntoEntregaUnicaYValido_PeroNoModoPorDefecto()
        Assert.IsTrue(ModosServicio.EsTodoJunto(6))
        Assert.IsTrue(ModosServicio.EsEntregaUnica(6))
        Assert.IsTrue(ModosServicio.EsValido(6))
        Assert.IsFalse(ModosServicio.EsValido(5))
        Assert.AreEqual(ModosServicio.POR_DEFECTO, ModosServicio.ParsearPorDefecto("6"))
        Assert.AreEqual(ModosServicio.TODO_JUNTO, ModosServicio.ParsearPorDefecto("1"))
        Assert.AreEqual(ModosServicio.AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ, ModosServicio.ParsearPorDefecto("4"))
    End Sub

    <TestMethod()>
    Public Sub Selector_ElSeisSoloSeVeSiLaApiLoOfreceOEsElDelPedido()
        Dim selector As New SelectorModosServicio()
        Dim seis = selector.Opciones.Single(Function(o) o.Codigo = ModosServicio.JUNTO_CON_LO_PENDIENTE)

        selector.Aplicar(Nothing)
        Assert.IsFalse(seis.Visible, "Sin respuesta, no aparece")
        Assert.IsTrue(selector.Opciones.Where(Function(o) o.Codigo <> 6).All(Function(o) o.Visible))

        ' API vieja (sin el 6) o contacto sin nada pendiente: no aparece, ni deshabilitado.
        selector.Aplicar(New ModoServicioSugeridoDTO With {.Modo = 3, .ModosPermitidos = New List(Of Byte) From {1, 2, 3, 4}})
        Assert.IsFalse(seis.Visible)

        ' La API lo ofrece.
        selector.Aplicar(New ModoServicioSugeridoDTO With {
            .Modo = 3, .ModosPermitidos = New List(Of Byte) From {1, 2, 3, 4, 6},
            .Modos = New List(Of ModoServicioPermitidoDTO) From {
                New ModoServicioPermitidoDTO With {.Modo = 6, .Permitido = True, .Explicacion = "Saldrá junto con la nota de entrega 929701.", .PedidosConLosQueSeJunta = New List(Of Integer) From {929701}}}})
        Assert.IsTrue(seis.Visible)
        Assert.IsTrue(seis.Habilitado)
        Assert.AreEqual("Saldrá junto con la nota de entrega 929701.", selector.Explicacion(6))

        ' Deja de ofrecerlo, pero es el modo del pedido: se sigue viendo (el combo no se queda en blanco).
        selector.Aplicar(New ModoServicioSugeridoDTO With {.Modo = 3, .ModosPermitidos = New List(Of Byte) From {1, 2, 3, 4}})
        Assert.IsFalse(seis.Visible)
        selector.ModoActual = ModosServicio.JUNTO_CON_LO_PENDIENTE
        Assert.IsTrue(seis.Visible)
        Assert.IsNull(selector.Explicacion(6))
        selector.ModoActual = ModosServicio.TODO_JUNTO
        Assert.IsFalse(seis.Visible)
    End Sub

    <TestMethod()>
    Public Sub Wrapper_ConElSeis_MarcarServirJuntoNoLoPasaAlUno()
        Dim wrapper As New Nesto.Modulos.PedidoVenta.PedidoVentaWrapper(New PedidoVentaDTO With {.empresa = "1", .numero = 1, .modoServicio = 6, .servirJunto = True})

        Assert.AreEqual(ModosServicio.JUNTO_CON_LO_PENDIENTE, wrapper.ModoServicio)
        wrapper.servirJunto = True
        Assert.AreEqual(CType(6, Byte?), wrapper.Model.modoServicio)
        wrapper.ModoServicio = ModosServicio.TODO_JUNTO
        Assert.AreEqual(CType(1, Byte?), wrapper.Model.modoServicio)
        wrapper.ModoServicio = ModosServicio.JUNTO_CON_LO_PENDIENTE
        Assert.AreEqual(CType(6, Byte?), wrapper.Model.modoServicio)
        Assert.IsTrue(wrapper.Model.servirJunto)
    End Sub
End Class
