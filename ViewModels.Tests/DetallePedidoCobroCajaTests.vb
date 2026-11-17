Imports FakeItEasy
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.Modulos.PedidoVenta.Models.Facturas
Imports CommunityToolkit.Mvvm.Messaging
Imports System.Collections.ObjectModel
Imports System.Threading.Tasks
Imports ControlesUsuario.Dialogs

''' <summary>
''' Nesto#645 (Novedades 1317 y 1296): al facturar en la tienda, primero el cobro en caja (con el efecto de la factura marcado) y
''' después la impresión, para que la factura no salga con «Pendiente de pago».
''' </summary>
<TestClass()>
Public Class DetallePedidoCobroCajaTests

    Private servicio As IPedidoVentaService
    Private dialogService As IServicioDialogos
    Private configuracion As IConfiguracion

    <TestInitialize()>
    Public Sub Initialize()
        servicio = A.Fake(Of IPedidoVentaService)
        dialogService = A.Fake(Of IServicioDialogos)
        configuracion = A.Fake(Of IConfiguracion)
        ' «¿Desea abrir el documento de la factura?»: no, para no abrir nada en los tests
        A.CallTo(Function() dialogService.ShowConfirmationAnswer("Abrir factura", A(Of String).Ignored)).Returns(False)
    End Sub

    Private Function CrearVm(esTienda As Boolean) As DetallePedidoViewModel
        A.CallTo(Function() configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDAS)).Returns(esTienda)
        Dim vm As New DetallePedidoViewModel(A.Fake(Of IServicioNavegacion), configuracion, servicio, New WeakReferenceMessenger(),
                                             dialogService, A.Fake(Of IAbridorPedidos), A.Fake(Of IServicioAutenticacion))
        vm.pedido = New PedidoVentaWrapper(New PedidoVentaDTO With {
            .empresa = "1", .numero = 930905, .cliente = "29064", .contacto = "0",
            .Lineas = New ObservableCollection(Of LineaPedidoVentaDTO)})
        Return vm
    End Function

    Private Shared Function Factura(Optional datos As DocumentoParaImprimir = Nothing) As CrearFacturaResponseDTO
        Return New CrearFacturaResponseDTO With {.NumeroFactura = "NV26/000200", .Empresa = "1", .NumeroPedido = 930905, .DatosImpresion = datos}
    End Function

    Private Sub Pendiente(importe As Decimal)
        A.CallTo(Function() servicio.LeerPendienteDocumento("1", "29064", "NV26/000200")).Returns(Task.FromResult(importe))
    End Sub

    Private Sub ContestaCobroEnCaja(respuesta As Boolean)
        A.CallTo(Function() dialogService.ShowConfirmationAnswer("Cobro en caja", A(Of String).Ignored)).Returns(respuesta)
    End Sub

    Private Sub ElCobroTermina(boton As ResultadoBoton)
        A.CallTo(Function() dialogService.ShowDialogAsync(CobroEnCajaFactura.DIALOGO, A(Of ParametrosDialogo).Ignored)).
            Returns(Task.FromResult(New ResultadoDialogo(boton)))
    End Sub

    <TestMethod()>
    Public Async Function Tienda_ConAlgoPorCobrar_DiceQueSi_AbreElCobroConLaFacturaYDespuesImprime() As Task
        Pendiente(30.25D)
        ContestaCobroEnCaja(True)
        ElCobroTermina(ResultadoBoton.OK)
        Dim parametros As ParametrosDialogo = Nothing
        A.CallTo(Function() dialogService.ShowDialogAsync(CobroEnCajaFactura.DIALOGO, A(Of ParametrosDialogo).Ignored)).
            Invokes(Sub(nombre As String, p As ParametrosDialogo) parametros = p).
            Returns(Task.FromResult(New ResultadoDialogo(ResultadoBoton.OK)))
        Dim vm = CrearVm(esTienda:=True)

        Await vm.TrasCrearFacturaAsync(Factura(), "29064", "0")

        Assert.IsNotNull(parametros, "Se abre el cobro")
        Assert.AreEqual("NV26/000200", parametros.GetValue(Of String)(CobroEnCajaFactura.PARAMETRO_DOCUMENTO))
        Assert.AreEqual("1", parametros.GetValue(Of String)(CobroEnCajaFactura.PARAMETRO_EMPRESA))
        Assert.AreEqual("29064", parametros.GetValue(Of String)(CobroEnCajaFactura.PARAMETRO_CLIENTE))
        Assert.AreEqual("0", parametros.GetValue(Of String)(CobroEnCajaFactura.PARAMETRO_CONTACTO))
        ' Primero se cobra, después se imprime (Carlos 17/11/26)
        A.CallTo(Function() dialogService.ShowConfirmationAnswer("Cobro en caja", A(Of String).That.Contains("NV26/000200"))).MustHaveHappenedOnceExactly().
            Then(A.CallTo(Function() dialogService.ShowDialogAsync(CobroEnCajaFactura.DIALOGO, A(Of ParametrosDialogo).Ignored)).MustHaveHappenedOnceExactly()).
            Then(A.CallTo(Function() dialogService.ShowConfirmationAnswer("Abrir factura", A(Of String).Ignored)).MustHaveHappenedOnceExactly())
    End Function

    <TestMethod()>
    Public Async Function Tienda_DiceQueNo_NoAbreElCobroPeroImprimeIgual() As Task
        Pendiente(30.25D)
        ContestaCobroEnCaja(False)
        Dim vm = CrearVm(esTienda:=True)

        Await vm.TrasCrearFacturaAsync(Factura(), "29064", "0")

        A.CallTo(Function() dialogService.ShowDialogAsync(A(Of String).Ignored, A(Of ParametrosDialogo).Ignored)).MustNotHaveHappened()
        A.CallTo(Function() dialogService.ShowConfirmationAnswer("Abrir factura", A(Of String).Ignored)).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function NoEsDeTienda_NoPreguntaNiLeeElExtracto() As Task
        Dim vm = CrearVm(esTienda:=False)

        Dim cobrado = Await vm.OfrecerCobroEnCajaAsync(Factura(), "29064", "0")

        Assert.IsFalse(cobrado)
        A.CallTo(Function() servicio.LeerPendienteDocumento(A(Of String).Ignored, A(Of String).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
        A.CallTo(Function() dialogService.ShowConfirmationAnswer("Cobro en caja", A(Of String).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function Tienda_FacturaSinNadaPorCobrar_NoPregunta() As Task
        Pendiente(0D) ' prepago que la cubre o ya cobrada
        Dim vm = CrearVm(esTienda:=True)

        Dim cobrado = Await vm.OfrecerCobroEnCajaAsync(Factura(), "29064", "0")

        Assert.IsFalse(cobrado)
        A.CallTo(Function() dialogService.ShowConfirmationAnswer("Cobro en caja", A(Of String).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function Tienda_SinPoderLeerLoPendiente_PreguntaIgual() As Task
        A.CallTo(Function() servicio.LeerPendienteDocumento(A(Of String).Ignored, A(Of String).Ignored, A(Of String).Ignored)).
            ThrowsAsync(New Exception("sin red"))
        ContestaCobroEnCaja(False)
        Dim vm = CrearVm(esTienda:=True)

        Await vm.OfrecerCobroEnCajaAsync(Factura(), "29064", "0")

        A.CallTo(Function() dialogService.ShowConfirmationAnswer("Cobro en caja", A(Of String).Ignored)).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function Tienda_FinDeMes_NoPregunta() As Task
        Dim vm = CrearVm(esTienda:=True)
        Dim fdm = Factura()
        fdm.NumeroFactura = Constantes.PeriodosFacturacion.FIN_DE_MES

        Dim cobrado = Await vm.OfrecerCobroEnCajaAsync(fdm, "29064", "0")

        Assert.IsFalse(cobrado)
        A.CallTo(Function() servicio.LeerPendienteDocumento(A(Of String).Ignored, A(Of String).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function Tienda_CierraElCobroSinCobrar_DevuelveQueNoSeHaCobrado() As Task
        Pendiente(30.25D)
        ContestaCobroEnCaja(True)
        ElCobroTermina(ResultadoBoton.Cancel)
        Dim vm = CrearVm(esTienda:=True)

        Dim cobrado = Await vm.OfrecerCobroEnCajaAsync(Factura(), "29064", "0")

        Assert.IsFalse(cobrado)
    End Function

    <TestMethod()>
    Public Async Function Cobrado_LaFacturaEnPapelSePideOtraVezParaQueSalgaCobrada() As Task
        Pendiente(30.25D)
        ContestaCobroEnCaja(True)
        ElCobroTermina(ResultadoBoton.OK)
        Dim nuevos As Byte() = {9, 9, 9}
        Dim documentos As New DocumentosImpresionPedidoDTO()
        documentos.Facturas.Add(New FacturaCreadaDTO With {.NumeroFactura = "NV26/000200", .DatosImpresion = New DocumentoParaImprimir With {.BytesPDF = nuevos}})
        A.CallTo(Function() servicio.ObtenerDocumentosImpresion("1", 930905, "NV26/000200", A(Of Integer?).Ignored)).Returns(Task.FromResult(documentos))
        A.CallTo(Function() dialogService.ShowConfirmationAnswer("Factura en papel", A(Of String).Ignored)).Returns(False)
        Dim datos As New DocumentoParaImprimir With {.BytesPDF = {1, 2, 3}, .NumeroCopias = 2}
        Dim vm = CrearVm(esTienda:=True)

        Await vm.TrasCrearFacturaAsync(Factura(datos), "29064", "0")

        CollectionAssert.AreEqual(nuevos, datos.BytesPDF, "Se imprime la de después del cobro")
    End Function

    <TestMethod()>
    Public Async Function SinCobrar_LaFacturaEnPapelNoSePideOtraVez() As Task
        Pendiente(30.25D)
        ContestaCobroEnCaja(False)
        A.CallTo(Function() dialogService.ShowConfirmationAnswer("Factura en papel", A(Of String).Ignored)).Returns(False)
        Dim vm = CrearVm(esTienda:=True)

        Await vm.TrasCrearFacturaAsync(Factura(New DocumentoParaImprimir With {.BytesPDF = {1}}), "29064", "0")

        A.CallTo(Function() servicio.ObtenerDocumentosImpresion(A(Of String).Ignored, A(Of Integer).Ignored, A(Of String).Ignored, A(Of Integer?).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Sub DebePreguntar_Regla()
        Assert.IsFalse(CobroEnCajaFactura.DebePreguntar(False, 10D))
        Assert.IsFalse(CobroEnCajaFactura.DebePreguntar(True, 0D))
        Assert.IsFalse(CobroEnCajaFactura.DebePreguntar(True, -5D), "Un abono no se cobra")
        Assert.IsTrue(CobroEnCajaFactura.DebePreguntar(True, 10D))
        Assert.IsTrue(CobroEnCajaFactura.DebePreguntar(True, Nothing), "Sin saberlo, se pregunta")
    End Sub

    <TestMethod()>
    Public Sub SumarPendienteDocumento_SoloEseDocumentoYEmpresa()
        Dim efectos As New List(Of PedidoVentaService.EfectoPendienteDocumento) From {
            New PedidoVentaService.EfectoPendienteDocumento With {.Empresa = "1", .Documento = "NV26/000200 ", .ImportePendiente = 20D},
            New PedidoVentaService.EfectoPendienteDocumento With {.Empresa = "1", .Documento = "NV26/000200", .ImportePendiente = 10.25D},
            New PedidoVentaService.EfectoPendienteDocumento With {.Empresa = "3", .Documento = "NV26/000200", .ImportePendiente = 7D},
            New PedidoVentaService.EfectoPendienteDocumento With {.Empresa = "1", .Documento = "NV26/000100", .ImportePendiente = 50D}
        }
        Assert.AreEqual(30.25D, PedidoVentaService.SumarPendienteDocumento(efectos, "1", "NV26/000200"))
        Assert.AreEqual(0D, PedidoVentaService.SumarPendienteDocumento(Nothing, "1", "NV26/000200"))
    End Sub
End Class
