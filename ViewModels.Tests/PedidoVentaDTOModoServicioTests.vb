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
    Public Sub ModosServicio_Lista_OfreceLosCuatroModosConNombre()
        Dim codigos = ModosServicio.Lista.Select(Function(m) m.Codigo).ToList()

        CollectionAssert.AreEqual(New List(Of Byte) From {1, 2, 3, 4}, codigos)
        Assert.IsTrue(ModosServicio.Lista.All(Function(m) Not String.IsNullOrWhiteSpace(m.Nombre) AndAlso Not String.IsNullOrWhiteSpace(m.Descripcion)))
        Assert.AreEqual("Tras reponer de tiendas", ModosServicio.Nombre(ModosServicio.TRAS_REPONER_DE_TIENDAS))
    End Sub
End Class
