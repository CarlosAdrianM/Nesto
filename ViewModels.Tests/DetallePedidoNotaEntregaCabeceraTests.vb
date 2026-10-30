Imports FakeItEasy
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models
Imports Nesto.Modulos.PedidoVenta
Imports CommunityToolkit.Mvvm.Messaging
Imports System.Collections.ObjectModel
Imports System.Threading.Tasks
Imports ControlesUsuario.Dialogs
Imports ControlesUsuario.Models

''' <summary>
''' Nesto#579 (NestoAPI#757): panel «Nota de entrega» del detalle (tipo, bultos y factura original, guardados al momento por
''' PUT api/NotasEntrega/{empresa}/{pedido}/Cabecera), la casilla en un pedido nuevo y «Nueva nota de recogida».
''' Bultos y factura solo en las recogidas: las entregas pasan por el packing (Carlos, 30/10/26).
''' </summary>
<TestClass()>
Public Class DetallePedidoNotaEntregaCabeceraTests

    Private servicio As IPedidoVentaService
    Private dialogService As IServicioDialogos
    Private configuracion As IConfiguracion

    <TestInitialize()>
    Public Sub Initialize()
        servicio = A.Fake(Of IPedidoVentaService)
        dialogService = A.Fake(Of IServicioDialogos)
        configuracion = A.Fake(Of IConfiguracion)
    End Sub

    Private Function CrearVm(ParamArray grupos As String()) As DetallePedidoViewModel
        For Each grupo In grupos
            A.CallTo(Function() configuracion.UsuarioEnGrupo(grupo)).Returns(True)
        Next
        Return New DetallePedidoViewModel(A.Fake(Of IServicioNavegacion), configuracion, servicio, New WeakReferenceMessenger(),
                                          dialogService, A.Fake(Of IAbridorPedidos), A.Fake(Of IServicioAutenticacion))
    End Function

    Private Shared Function Nota(Optional tipo As String = Nothing, Optional bultos As Integer? = Nothing, Optional factura As String = Nothing) As PedidoVentaWrapper
        Return New PedidoVentaWrapper(New PedidoVentaDTO With {
            .empresa = "1", .numero = 927519, .cliente = "15191", .contacto = "0", .notaEntrega = True,
            .tipoNotaEntrega = tipo, .bultosNota = bultos, .facturaOriginalNota = factura,
            .Lineas = New ObservableCollection(Of LineaPedidoVentaDTO)})
    End Function

    Private Shared Function PedidoNormal(numero As Integer) As PedidoVentaWrapper
        Return New PedidoVentaWrapper(New PedidoVentaDTO With {
            .empresa = "1", .numero = numero, .cliente = "15191", .contacto = "0", .notaEntrega = False,
            .Lineas = New ObservableCollection(Of LineaPedidoVentaDTO)})
    End Function

    Private Sub ApiDevuelveLoQueRecibe()
        A.CallTo(Function() servicio.CambiarCabeceraNotaEntrega(A(Of String).Ignored, A(Of Integer).Ignored, A(Of String).Ignored, A(Of Integer).Ignored, A(Of String).Ignored)).
            ReturnsLazily(Function(empresa As String, pedido As Integer, tipo As String, bultos As Integer, factura As String) _
                              Task.FromResult(New NotaEntregaCabeceraDTO With {.Empresa = empresa, .Pedido = pedido, .Tipo = tipo, .Bultos = bultos, .FacturaOriginal = factura}))
    End Sub

    Private Sub ApiRechaza(mensaje As String)
        A.CallTo(Function() servicio.CambiarCabeceraNotaEntrega(A(Of String).Ignored, A(Of Integer).Ignored, A(Of String).Ignored, A(Of Integer).Ignored, A(Of String).Ignored)).
            ThrowsAsync(New Exception(mensaje))
    End Sub

#Region "Panel"
    <TestMethod()>
    Public Sub Panel_SoloSeVeEnLasNotas()
        Dim vm = CrearVm()
        vm.pedido = Nota()
        Assert.IsTrue(vm.MostrarPanelNotaEntrega)

        vm.pedido = PedidoNormal(927520)
        Assert.IsFalse(vm.MostrarPanelNotaEntrega)
        Assert.IsFalse(vm.EsNotaRecogida)
    End Sub

    <TestMethod()>
    Public Sub Panel_NotaDeNestoViejoSinFila_EsEntregaConUnBultoYSinBultosNiFactura()
        Dim vm = CrearVm()
        vm.pedido = Nota()

        Assert.AreEqual(TiposNotaEntrega.ENTREGA, vm.TipoNota)
        Assert.AreEqual(1, vm.BultosNota)
        Assert.IsFalse(vm.EsNotaRecogida, "Con Entrega no se ven ni Bultos ni Factura original")
        Assert.AreEqual("Nota de entrega NE927519", vm.TextoCabeceraNota)
    End Sub

    <TestMethod()>
    Public Sub Panel_Recogida_EnsenaBultosYFacturaConLoQueTraeElPedido()
        Dim vm = CrearVm()
        vm.pedido = Nota("Recogida", 3, "NV26/001234 ")

        Assert.AreEqual(TiposNotaEntrega.RECOGIDA, vm.TipoNota)
        Assert.IsTrue(vm.EsNotaRecogida)
        Assert.AreEqual(3, vm.BultosNota)
        Assert.AreEqual("NV26/001234", vm.FacturaOriginalNota)
        Assert.AreEqual("Nota de recogida NR927519", vm.TextoCabeceraNota)
    End Sub

    <TestMethod()>
    Public Sub Panel_AlCargarNoLlamaALaApi()
        Dim vm = CrearVm()
        vm.pedido = Nota("Recogida", 2)

        A.CallTo(Function() servicio.CambiarCabeceraNotaEntrega(A(Of String).Ignored, A(Of Integer).Ignored, A(Of String).Ignored, A(Of Integer).Ignored, A(Of String).Ignored)).
            MustNotHaveHappened()
    End Sub
#End Region

#Region "Guardar al momento"
    <TestMethod()>
    Public Async Function CambiarTipo_EnNotaGrabada_VaPorElPutYQuedaEnElPedido() As Task
        ApiDevuelveLoQueRecibe()
        Dim vm = CrearVm()
        vm.pedido = Nota()

        vm.TipoNota = TiposNotaEntrega.RECOGIDA
        Await vm.CambioCabeceraNotaEnCurso

        A.CallTo(Function() servicio.CambiarCabeceraNotaEntrega("1", 927519, "Recogida", 1, Nothing)).MustHaveHappenedOnceExactly()
        Assert.IsTrue(vm.EsNotaRecogida)
        Assert.AreEqual("Recogida", vm.pedido.Model.tipoNotaEntrega)
    End Function

    <TestMethod()>
    Public Async Function CambiarTipo_SiLaApiLoRechaza_EnsenaSuMensajeYVuelveAtras() As Task
        ApiRechaza("La parada de esta nota ya está entregada: no se puede cambiar.")
        Dim vm = CrearVm()
        vm.pedido = Nota()

        vm.TipoNota = TiposNotaEntrega.RECOGIDA
        Await vm.CambioCabeceraNotaEnCurso

        A.CallTo(Sub() dialogService.ShowError("La parada de esta nota ya está entregada: no se puede cambiar.")).MustHaveHappenedOnceExactly()
        Assert.AreEqual(TiposNotaEntrega.ENTREGA, vm.TipoNota)
        Assert.IsFalse(vm.EsNotaRecogida)
    End Function

    <TestMethod()>
    Public Async Function CambiarBultos_EnRecogida_VaPorElPut() As Task
        ApiDevuelveLoQueRecibe()
        Dim vm = CrearVm()
        vm.pedido = Nota("Recogida", 1)

        vm.BultosNota = 4
        Await vm.CambioCabeceraNotaEnCurso

        A.CallTo(Function() servicio.CambiarCabeceraNotaEntrega("1", 927519, "Recogida", 4, Nothing)).MustHaveHappenedOnceExactly()
        Assert.AreEqual(4, vm.BultosNota)
    End Function

    <TestMethod()>
    Public Async Function CambiarBultos_SiLaApiLoRechaza_VuelveAlValorAnterior() As Task
        ApiRechaza("Una nota de recogida tiene que llevar al menos un bulto.")
        Dim vm = CrearVm()
        vm.pedido = Nota("Recogida", 2)

        vm.BultosNota = 0
        Await vm.CambioCabeceraNotaEnCurso

        A.CallTo(Sub() dialogService.ShowError("Una nota de recogida tiene que llevar al menos un bulto.")).MustHaveHappenedOnceExactly()
        Assert.AreEqual(2, vm.BultosNota)
    End Function

    <TestMethod()>
    Public Async Function VincularFactura_VaPorElPutConLaFacturaElegida() As Task
        ApiDevuelveLoQueRecibe()
        Dim vm = CrearVm()
        vm.pedido = Nota("Recogida", 1)
        vm.MostrarSelectorFacturaOriginal = True
        vm.FacturaOriginalEnLista = New FacturaClienteDTO With {.Documento = "NV26/004321"}

        vm.VincularFacturaOriginal()
        Await vm.CambioCabeceraNotaEnCurso

        A.CallTo(Function() servicio.CambiarCabeceraNotaEntrega("1", 927519, "Recogida", 1, "NV26/004321")).MustHaveHappenedOnceExactly()
        Assert.AreEqual("NV26/004321", vm.FacturaOriginalNota)
        Assert.IsFalse(vm.MostrarSelectorFacturaOriginal, "El selector se cierra al vincular")
    End Function

    <TestMethod()>
    Public Async Function VincularFactura_DeOtroCliente_EnsenaElMensajeYQuedaSinVincular() As Task
        ApiRechaza("La factura NV26/009999 no es de este cliente.")
        Dim vm = CrearVm()
        vm.pedido = Nota("Recogida", 1)
        vm.FacturaOriginalEnLista = New FacturaClienteDTO With {.Documento = "NV26/009999"}

        vm.VincularFacturaOriginal()
        Await vm.CambioCabeceraNotaEnCurso

        A.CallTo(Sub() dialogService.ShowError("La factura NV26/009999 no es de este cliente.")).MustHaveHappenedOnceExactly()
        Assert.IsNull(vm.FacturaOriginalNota)
    End Function

    <TestMethod()>
    Public Async Function QuitarFactura_VaPorElPutSinFactura() As Task
        ApiDevuelveLoQueRecibe()
        Dim vm = CrearVm()
        vm.pedido = Nota("Recogida", 1, "NV26/004321")

        vm.QuitarFacturaOriginal()
        Await vm.CambioCabeceraNotaEnCurso

        A.CallTo(Function() servicio.CambiarCabeceraNotaEntrega("1", 927519, "Recogida", 1, Nothing)).MustHaveHappenedOnceExactly()
        Assert.AreEqual("(sin vincular)", vm.TextoFacturaOriginal)
    End Function
#End Region

#Region "Casilla en pedido nuevo"
    <TestMethod()>
    Public Sub Casilla_HabilitadaSoloEnPedidoNuevoYParaLosGrupos()
        For Each grupo In {Constantes.GruposSeguridad.ALMACEN, Constantes.GruposSeguridad.ADMINISTRACION,
                           Constantes.GruposSeguridad.DIRECCION, Constantes.GruposSeguridad.INFORMATICA}
            configuracion = A.Fake(Of IConfiguracion)
            Dim vm = CrearVm(grupo)
            vm.pedido = PedidoNormal(0)
            Assert.IsTrue(vm.PuedeMarcarNotaEntrega, $"{grupo} puede crear notas")

            vm.pedido = PedidoNormal(927520)
            Assert.IsFalse(vm.PuedeMarcarNotaEntrega, $"{grupo}: un pedido grabado no se pasa a nota")
        Next
    End Sub

    <TestMethod()>
    Public Sub Casilla_DeshabilitadaParaOtrosGrupos()
        Dim vm = CrearVm(Constantes.GruposSeguridad.TIENDAS)
        vm.pedido = PedidoNormal(0)

        Assert.IsFalse(vm.PuedeMarcarNotaEntrega)
        vm.NotaEntregaMarcada = True
        Assert.IsFalse(vm.pedido.notaEntrega, "Sin permiso no se marca aunque se intente")
        Assert.IsFalse(vm.NuevaNotaRecogidaCommand.CanExecute(Nothing))
    End Sub

    <TestMethod()>
    Public Sub Casilla_AlMarcarla_SaleElPanelComoEntregaSinModoDeFacturacionYSinLlamarALaApi()
        Dim vm = CrearVm(Constantes.GruposSeguridad.ALMACEN)
        vm.pedido = PedidoNormal(0)

        vm.NotaEntregaMarcada = True

        Assert.IsTrue(vm.pedido.notaEntrega)
        Assert.IsTrue(vm.MostrarPanelNotaEntrega)
        Assert.AreEqual(TiposNotaEntrega.ENTREGA, vm.TipoNota)
        Assert.IsFalse(vm.PuedeElegirModoFacturacion)
        Assert.AreEqual("Nota de entrega", vm.TextoCabeceraNota)

        vm.TipoNota = TiposNotaEntrega.RECOGIDA
        Assert.AreEqual("Recogida", vm.pedido.Model.tipoNotaEntrega)
        Assert.AreEqual(1, vm.pedido.Model.bultosNota)
        A.CallTo(Function() servicio.CambiarCabeceraNotaEntrega(A(Of String).Ignored, A(Of Integer).Ignored, A(Of String).Ignored, A(Of Integer).Ignored, A(Of String).Ignored)).
            MustNotHaveHappened()

        vm.NotaEntregaMarcada = False
        Assert.IsFalse(vm.MostrarPanelNotaEntrega)
        Assert.IsNull(vm.pedido.Model.tipoNotaEntrega)
        Assert.IsTrue(vm.PuedeElegirModoFacturacion)
    End Sub

    <TestMethod()>
    Public Sub Casilla_EntregaNueva_NoMandaBultos()
        Dim vm = CrearVm(Constantes.GruposSeguridad.ALMACEN)
        vm.pedido = PedidoNormal(0)

        vm.NotaEntregaMarcada = True

        Assert.AreEqual("Entrega", vm.pedido.Model.tipoNotaEntrega)
        Assert.IsNull(vm.pedido.Model.bultosNota, "En la entrega los bultos salen del packing")
    End Sub
#End Region

#Region "Nueva nota de recogida"
    <TestMethod()>
    Public Async Function NuevaNotaRecogida_MontaElPedidoConElClienteActualRecogidaUnBultoYUnaLineaDeTextoConElFoco() As Task
        Dim vm = CrearVm(Constantes.GruposSeguridad.ALMACEN)
        vm.pedido = PedidoNormal(927520)
        Dim lineaConFoco As LineaPedidoVentaWrapper = Nothing
        AddHandler vm.FocoEnTextoDeLineaSolicitado, Sub(s, l) lineaConFoco = l

        Assert.IsTrue(vm.NuevaNotaRecogidaCommand.CanExecute(Nothing))
        Await vm.NuevaNotaRecogidaAsync()

        Assert.AreEqual(0, vm.pedido.numero, "Es un pedido nuevo")
        Assert.AreEqual("15191", vm.pedido.cliente)
        Assert.AreEqual("0", vm.pedido.contacto)
        Assert.IsTrue(vm.pedido.notaEntrega)
        Assert.IsTrue(vm.NotaEntregaMarcada)
        Assert.AreEqual(TiposNotaEntrega.RECOGIDA, vm.TipoNota)
        Assert.IsTrue(vm.EsNotaRecogida)
        Assert.AreEqual(1, vm.BultosNota)
        Assert.AreEqual(1, vm.pedido.Lineas.Count)
        Dim linea = vm.pedido.Lineas.Single()
        Assert.AreEqual(CByte(0), linea.tipoLinea.Value, "Línea de texto")
        Assert.AreEqual(1, vm.pedido.Model.Lineas.Count, "La línea está también en el modelo que va al POST")
        Assert.AreSame(linea, vm.lineaActual)
        Assert.AreSame(linea, lineaConFoco, "La vista pone el foco en el texto de esa línea")
    End Function

    <TestMethod()>
    Public Async Function NuevaNotaRecogida_AlGrabar_ElPostLlevaNotaEntregaTipoYBultos() As Task
        Dim enviado As PedidoVentaDTO = Nothing
        A.CallTo(Function() servicio.CrearPedido(A(Of PedidoVentaDTO).Ignored)).
            ReturnsLazily(Function(p As PedidoVentaDTO)
                              enviado = p
                              Return Task.FromResult(930001)
                          End Function)
        Dim vm = CrearVm(Constantes.GruposSeguridad.ALMACEN)
        vm.pedido = PedidoNormal(927520)
        Await vm.NuevaNotaRecogidaAsync()
        vm.pedido.Lineas.Single().texto = "Recoger el secador en garantía"

        Await vm.ModificarPedidoAsync()

        Assert.IsNotNull(enviado, "Se ha llamado al POST")
        Assert.IsTrue(enviado.notaEntrega)
        Assert.AreEqual("Recogida", enviado.tipoNotaEntrega)
        Assert.AreEqual(1, enviado.bultosNota)
        Assert.AreEqual("15191", enviado.cliente)
        Assert.AreEqual(930001, vm.pedido.numero)
        Assert.IsFalse(vm.PuedeMarcarNotaEntrega, "Ya grabada, la casilla no se toca")
        A.CallTo(Function() servicio.CambiarCabeceraNotaEntrega(A(Of String).Ignored, A(Of Integer).Ignored, A(Of String).Ignored, A(Of Integer).Ignored, A(Of String).Ignored)).
            MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function NuevaNotaRecogida_ConFacturaElegida_LaVinculaNadaMasCrearla() As Task
        ApiDevuelveLoQueRecibe()
        A.CallTo(Function() servicio.CrearPedido(A(Of PedidoVentaDTO).Ignored)).Returns(Task.FromResult(930002))
        Dim vm = CrearVm(Constantes.GruposSeguridad.ALMACEN)
        vm.pedido = PedidoNormal(927520)
        Await vm.NuevaNotaRecogidaAsync()
        vm.FacturaOriginalEnLista = New FacturaClienteDTO With {.Documento = "NV26/004321"}
        vm.VincularFacturaOriginal()
        A.CallTo(Function() servicio.CambiarCabeceraNotaEntrega(A(Of String).Ignored, A(Of Integer).Ignored, A(Of String).Ignored, A(Of Integer).Ignored, A(Of String).Ignored)).
            MustNotHaveHappened() ' aún no está grabada

        Await vm.ModificarPedidoAsync()

        A.CallTo(Function() servicio.CambiarCabeceraNotaEntrega("1", 930002, "Recogida", 1, "NV26/004321")).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function NuevaNotaRecogida_SinCliente_NoSePuede() As Task
        Dim vm = CrearVm(Constantes.GruposSeguridad.ALMACEN)
        Dim sinCliente = PedidoNormal(0)
        sinCliente.cliente = String.Empty
        vm.pedido = sinCliente

        Assert.IsFalse(vm.NuevaNotaRecogidaCommand.CanExecute(Nothing))
        Await vm.NuevaNotaRecogidaAsync()
        Assert.IsFalse(vm.pedido.notaEntrega)
    End Function

    <TestMethod()>
    Public Async Function NuevaNotaRecogida_ConCambiosSinGuardarYNoConfirma_NoHaceNada() As Task
        A.CallTo(Function() dialogService.ShowConfirmationAsync(A(Of String).Ignored, A(Of String).Ignored)).Returns(Task.FromResult(False))
        Dim vm = CrearVm(Constantes.GruposSeguridad.ALMACEN)
        vm.pedido = PedidoNormal(0) ' un pedido nuevo siempre cuenta como «sin guardar»

        Await vm.NuevaNotaRecogidaAsync()

        Assert.IsFalse(vm.pedido.notaEntrega)
        Assert.AreEqual(0, vm.pedido.Lineas.Count)
    End Function
#End Region

End Class
