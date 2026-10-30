Imports FakeItEasy
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.Modulos.PedidoVenta.PedidoVentaModel
Imports CommunityToolkit.Mvvm.Messaging
Imports System.Collections.ObjectModel
Imports System.Threading.Tasks
Imports Newtonsoft.Json.Linq
Imports ControlesUsuario.Dialogs

''' <summary>
''' Nesto#581 (NestoAPI#760, corte 4 de NestoAPI#755): leer la etiqueta NR{Pedido}-{N} del bulto que trae el repartidor crea el
''' pedido de abono (serie RV, sin líneas) y Nesto lo abre en modo abono: líneas nuevas con cantidad −1 y el cursor en Producto.
''' </summary>
<TestClass()>
Public Class DetallePedidoAbonoRecogidaTests

    Private Const CODIGO As String = "NR929554-1"
    Private Const NOTA As Integer = 929554
    Private Const PEDIDO_ABONO As Integer = 930800

    Private servicio As IPedidoVentaService
    Private dialogService As IServicioDialogos
    Private configuracion As IConfiguracion

    <TestInitialize()>
    Public Sub Initialize()
        servicio = A.Fake(Of IPedidoVentaService)
        dialogService = A.Fake(Of IServicioDialogos)
        configuracion = A.Fake(Of IConfiguracion)
        A.CallTo(Function() servicio.cargarPedido(A(Of String).Ignored, A(Of Integer).Ignored)).
            ReturnsLazily(Function(empresa As String, numero As Integer) Task.FromResult(PedidoDto(numero, If(numero = PEDIDO_ABONO, "RV", "NV"))))
    End Sub

    Private Function CrearVm(Optional grupo As String = Constantes.GruposSeguridad.ALMACEN) As DetallePedidoViewModel
        If grupo IsNot Nothing Then
            A.CallTo(Function() configuracion.UsuarioEnGrupo(grupo)).Returns(True)
        End If
        Return New DetallePedidoViewModel(A.Fake(Of IServicioNavegacion), configuracion, servicio, New WeakReferenceMessenger(),
                                          dialogService, A.Fake(Of IAbridorPedidos), A.Fake(Of IServicioAutenticacion))
    End Function

    Private Shared Function PedidoDto(numero As Integer, serie As String) As PedidoVentaDTO
        Return New PedidoVentaDTO With {
            .empresa = "1", .numero = numero, .cliente = "15191", .contacto = "0", .serie = serie,
            .Lineas = New ObservableCollection(Of LineaPedidoVentaDTO)}
    End Function

    Private Shared Function Recogida(Optional pedidoAbono As Integer? = Nothing, Optional estado As String = "Entregado") As RecogidaDTO
        Return New RecogidaDTO With {
            .Empresa = "1", .PedidoNota = NOTA, .Cliente = "15191", .Contacto = "0", .Nombre = "PELUQUERÍA PRUEBA",
            .TextoNota = "Recoger secador averiado", .Bultos = 2, .FacturaOriginal = "NV26/001234", .EstadoEnvio = estado,
            .FechaRecogida = New Date(2026, 10, 30, 11, 15, 0), .Receptor = "María", .PedidoAbono = pedidoAbono}
    End Function

    Private Shared Function Abono(Optional yaExistia As Boolean = False) As AbonoRecogidaDTO
        Return New AbonoRecogidaDTO With {
            .Empresa = "1", .Pedido = PEDIDO_ABONO, .Cliente = "15191", .Contacto = "0", .Nombre = "PELUQUERÍA PRUEBA", .Serie = "RV",
            .PedidoNota = NOTA, .FacturaOriginal = "NV26/001234", .TextoNota = "Recoger secador averiado", .YaExistia = yaExistia}
    End Function

    Private Sub ApiLee(recogida As RecogidaDTO)
        A.CallTo(Function() servicio.LeerRecogida(A(Of String).Ignored)).Returns(Task.FromResult(recogida))
    End Sub

    Private Sub ApiCrea(forzar As Boolean, abono As AbonoRecogidaDTO)
        A.CallTo(Function() servicio.CrearAbonoRecogida(A(Of String).Ignored, forzar)).Returns(Task.FromResult(abono))
    End Sub

    Private Sub ApiNoRecogida(estado As String)
        A.CallTo(Function() servicio.CrearAbonoRecogida(A(Of String).Ignored, False)).
            ThrowsAsync(New RecogidaNoRecogidaException("La recogida de la nota 929554 no figura como recogida.", estado))
    End Sub

    Private Sub Responde(titulo As String, respuesta As Boolean)
        A.CallTo(Function() dialogService.ShowConfirmationAsync(titulo, A(Of String).Ignored)).Returns(Task.FromResult(respuesta))
    End Sub

#Region "Lectura → confirmación → POST → modo abono"
    <TestMethod()>
    Public Async Function Leer_RecogidaSinAbono_ConfirmaCreaYAbreEnModoAbono() As Task
        ApiLee(Recogida())
        ApiCrea(False, Abono())
        Responde("Crear abono", True)
        A.CallTo(Function() dialogService.GetCodigoBarras(A(Of String).Ignored, A(Of String).Ignored)).Returns(" " & CODIGO & " ")
        Dim vm = CrearVm()
        Dim focos As Integer = 0
        AddHandler vm.FocoEnProductoSolicitado, Sub(s, e) focos += 1

        Await vm.LeerEtiquetaRecogidaAsync()

        A.CallTo(Function() servicio.LeerRecogida(CODIGO)).MustHaveHappenedOnceExactly()
        A.CallTo(Function() dialogService.ShowConfirmationAsync("Crear abono", A(Of String).That.Contains("Recoger secador averiado"))).MustHaveHappenedOnceExactly()
        A.CallTo(Function() servicio.CrearAbonoRecogida(CODIGO, False)).MustHaveHappenedOnceExactly()
        A.CallTo(Function() servicio.CrearAbonoRecogida(A(Of String).Ignored, True)).MustNotHaveHappened()
        A.CallTo(Function() servicio.cargarPedido("1", PEDIDO_ABONO)).MustHaveHappenedOnceExactly()
        Assert.AreEqual(PEDIDO_ABONO, vm.pedido.numero)
        Assert.IsTrue(vm.EsModoAbonoRecogida)
        Assert.AreEqual(1, focos, "El cursor va a Producto")
        StringAssert.Contains(vm.TextoAvisoAbonoRecogida, "Abono por recogida de la nota 929554 — Recoger secador averiado")
        StringAssert.Contains(vm.TextoAvisoAbonoRecogida, "NV26/001234")
    End Function

    <TestMethod()>
    Public Async Function Leer_ElUsuarioCancelaLaConfirmacion_NoCreaNada() As Task
        ApiLee(Recogida())
        Responde("Crear abono", False)
        Dim vm = CrearVm()

        Await vm.AbrirAbonoDeRecogidaAsync(CODIGO)

        A.CallTo(Function() servicio.CrearAbonoRecogida(A(Of String).Ignored, A(Of Boolean).Ignored)).MustNotHaveHappened()
        A.CallTo(Function() servicio.cargarPedido(A(Of String).Ignored, A(Of Integer).Ignored)).MustNotHaveHappened()
        Assert.IsFalse(vm.EsModoAbonoRecogida)
    End Function

    <TestMethod()>
    Public Async Function Leer_SinCodigo_NoLlamaALaApi() As Task
        A.CallTo(Function() dialogService.GetCodigoBarras(A(Of String).Ignored, A(Of String).Ignored)).Returns(Nothing)
        Dim vm = CrearVm()

        Await vm.LeerEtiquetaRecogidaAsync()

        A.CallTo(Function() servicio.LeerRecogida(A(Of String).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function Leer_LaApiDiceQueNoEsUnaRecogida_EnseñaElErrorYNoCrea() As Task
        A.CallTo(Function() servicio.LeerRecogida(A(Of String).Ignored)).ThrowsAsync(New Exception("«NE929554» no es una etiqueta de recogida (NR…)."))
        Dim vm = CrearVm()

        Await vm.AbrirAbonoDeRecogidaAsync("NE929554")

        A.CallTo(Sub() dialogService.ShowError("«NE929554» no es una etiqueta de recogida (NR…).")).MustHaveHappenedOnceExactly()
        A.CallTo(Function() servicio.CrearAbonoRecogida(A(Of String).Ignored, A(Of Boolean).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function Leer_YaTeniaAbono_AbreElMismoSinPreguntarYLoDice() As Task
        ApiLee(Recogida(pedidoAbono:=PEDIDO_ABONO))
        ApiCrea(False, Abono(yaExistia:=True))
        Dim vm = CrearVm()

        Await vm.AbrirAbonoDeRecogidaAsync("NR929554-2")

        A.CallTo(Function() dialogService.ShowConfirmationAsync("Crear abono", A(Of String).Ignored)).MustNotHaveHappened()
        A.CallTo(Function() servicio.CrearAbonoRecogida("NR929554-2", False)).MustHaveHappenedOnceExactly()
        A.CallTo(Sub() dialogService.ShowNotification("Abono por recogida", A(Of String).That.Contains("ya estaba creado"))).MustHaveHappenedOnceExactly()
        Assert.AreEqual(PEDIDO_ABONO, vm.pedido.numero)
        Assert.IsTrue(vm.EsModoAbonoRecogida)
    End Function
#End Region

#Region "409: no figura como recogida → pregunta → Forzar"
    <TestMethod()>
    Public Async Function NoRecogida_ElUsuarioDiceQueSi_RepiteConForzar() As Task
        ApiLee(Recogida(estado:="Tramitado"))
        Responde("Crear abono", True)
        ApiNoRecogida("Tramitado")
        Responde("Recogida sin marcar", True)
        ApiCrea(True, Abono())
        Dim vm = CrearVm()

        Await vm.AbrirAbonoDeRecogidaAsync(CODIGO)

        A.CallTo(Function() dialogService.ShowConfirmationAsync("Recogida sin marcar",
            "La recogida de la nota 929554 no figura como recogida (estado Tramitado). ¿Crear el abono igualmente?")).MustHaveHappenedOnceExactly()
        A.CallTo(Function() servicio.CrearAbonoRecogida(CODIGO, True)).MustHaveHappenedOnceExactly()
        Assert.IsTrue(vm.EsModoAbonoRecogida)
    End Function

    <TestMethod()>
    Public Async Function NoRecogida_ElUsuarioDiceQueNo_NoFuerzaNiAbre() As Task
        ApiLee(Recogida(estado:=Nothing))
        Responde("Crear abono", True)
        ApiNoRecogida(RecogidaNoRecogidaException.ESTADO_SIN_ENVIO)
        Responde("Recogida sin marcar", False)
        Dim vm = CrearVm()

        Await vm.AbrirAbonoDeRecogidaAsync(CODIGO)

        A.CallTo(Function() dialogService.ShowConfirmationAsync("Recogida sin marcar", A(Of String).That.Contains("sin envío de reparto propio"))).MustHaveHappenedOnceExactly()
        A.CallTo(Function() servicio.CrearAbonoRecogida(A(Of String).Ignored, True)).MustNotHaveHappened()
        A.CallTo(Function() servicio.cargarPedido(A(Of String).Ignored, A(Of Integer).Ignored)).MustNotHaveHappened()
        Assert.IsFalse(vm.EsModoAbonoRecogida)
    End Function

    <TestMethod()>
    Public Async Function OtroErrorAlCrear_SeEnseñaYNoAbre() As Task
        ApiLee(Recogida())
        Responde("Crear abono", True)
        A.CallTo(Function() servicio.CrearAbonoRecogida(A(Of String).Ignored, False)).ThrowsAsync(New Exception("Su usuario no tiene permiso para las notas de entrega."))
        Dim vm = CrearVm()

        Await vm.AbrirAbonoDeRecogidaAsync(CODIGO)

        A.CallTo(Sub() dialogService.ShowError("No se ha podido crear el abono: Su usuario no tiene permiso para las notas de entrega.")).MustHaveHappenedOnceExactly()
        A.CallTo(Function() servicio.cargarPedido(A(Of String).Ignored, A(Of Integer).Ignored)).MustNotHaveHappened()
    End Function
#End Region

#Region "Modo abono: líneas con −1"
    <TestMethod()>
    Public Async Function ModoAbono_LineaNueva_ProductoYCantidadMenosUno() As Task
        ApiLee(Recogida(pedidoAbono:=PEDIDO_ABONO))
        ApiCrea(False, Abono(yaExistia:=True))
        Dim vm = CrearVm()
        Await vm.AbrirAbonoDeRecogidaAsync(CODIGO)
        Dim linea As New LineaPedidoVentaWrapper() With {.tipoLinea = CByte(0)}

        vm.PrepararLineaNueva(linea)

        Assert.AreEqual(CByte(1), linea.tipoLinea)
        Assert.AreEqual(CShort(-1), linea.Cantidad)
    End Function

    <TestMethod()>
    Public Sub SinModoAbono_LaLineaNuevaSigueConUno()
        Dim vm = CrearVm()
        vm.pedido = New PedidoVentaWrapper(PedidoDto(PEDIDO_ABONO, "RV"))
        Dim linea As New LineaPedidoVentaWrapper() With {.tipoLinea = CByte(1)}

        vm.PrepararLineaNueva(linea)

        Assert.IsFalse(vm.EsModoAbonoRecogida, "Abrir un RV a mano no es leer una recogida")
        Assert.AreEqual(CShort(1), linea.Cantidad)
    End Sub

    <TestMethod()>
    Public Async Function ModoAbono_AlCargarOtroPedido_SeApaga() As Task
        ApiLee(Recogida(pedidoAbono:=PEDIDO_ABONO))
        ApiCrea(False, Abono(yaExistia:=True))
        Dim vm = CrearVm()
        Await vm.AbrirAbonoDeRecogidaAsync(CODIGO)
        Assert.IsTrue(vm.EsModoAbonoRecogida)

        Await vm.CargarPedidoAsync(New ResumenPedido With {.empresa = "1", .numero = 930900})

        Assert.IsFalse(vm.EsModoAbonoRecogida)
        Assert.IsNull(vm.TextoAvisoAbonoRecogida)
    End Function
#End Region

#Region "Permisos, avisos del PUT y textos"
    <TestMethod()>
    Public Sub Permisos_SoloLosGruposDeLasNotas()
        Assert.IsFalse(CrearVm(grupo:=Nothing).LeerEtiquetaRecogidaCommand.CanExecute(Nothing), "Ventas no")
        For Each grupo In {Constantes.GruposSeguridad.ALMACEN, Constantes.GruposSeguridad.ADMINISTRACION,
                           Constantes.GruposSeguridad.DIRECCION, Constantes.GruposSeguridad.INFORMATICA}
            configuracion = A.Fake(Of IConfiguracion)
            Dim vm = CrearVm(grupo)
            Assert.IsTrue(vm.PuedeLeerEtiquetaRecogida, grupo)
            Assert.IsTrue(vm.LeerEtiquetaRecogidaCommand.CanExecute(Nothing), grupo)
        Next
    End Sub

    <TestMethod()>
    Public Async Function AvisoDelPut_ProductoFueraDeLaFactura_SeEnseñaSinBloquear() As Task
        Dim vm = CrearVm()
        Dim aviso As New AvisoPedidoModel With {
            .Tipo = AvisoPedidoModel.TIPO_ABONO_PRODUCTO_FUERA_DE_FACTURA,
            .Mensaje = "El producto 99999 no está en la factura NV26/001234.",
            .Datos = New JObject()}

        Await vm.ProcesarAvisosModificacion(New List(Of AvisoPedidoModel) From {aviso})

        A.CallTo(Sub() dialogService.ShowNotification("Producto fuera de la factura", "El producto 99999 no está en la factura NV26/001234.")).MustHaveHappenedOnceExactly()
        A.CallTo(Sub() dialogService.ShowError(A(Of String).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Sub DescribirRecogida_ClienteTextoBultosFacturaYQuienLaEntrego()
        Dim texto As String = DetallePedidoViewModel.DescribirRecogida(Recogida())

        StringAssert.Contains(texto, "Nota de recogida 929554")
        StringAssert.Contains(texto, "15191/0 — PELUQUERÍA PRUEBA")
        StringAssert.Contains(texto, "Recoger secador averiado")
        StringAssert.Contains(texto, "Bultos: 2")
        StringAssert.Contains(texto, "NV26/001234")
        StringAssert.Contains(texto, "el 30/10/2026 a las 11:15 (la entregó María)")
        StringAssert.Contains(texto, "¿Crear el pedido de abono?")
    End Sub

    <TestMethod()>
    Public Sub DescribirRecogida_SinRecogerYSinFactura()
        Dim r As RecogidaDTO = Recogida(estado:="Tramitado")
        r.FacturaOriginal = Nothing

        Dim texto As String = DetallePedidoViewModel.DescribirRecogida(r)

        StringAssert.Contains(texto, "aún no la ha marcado como recogida (estado Tramitado)")
        StringAssert.Contains(texto, "(ninguna; se vinculará por las últimas compras)")
    End Sub

    <TestMethod()>
    Public Sub LeerNoRecogida_ElFormatoDeErrorDeLaApi()
        Dim body As String = "{""error"":{""code"":""RECOGIDA_NO_RECOGIDA"",""message"":""La recogida de la nota 929554 no figura como recogida (estado Incidentado)."",""details"":{""estado"":""Incidentado"",""envio"":251400}},""Message"":""x""}"

        Dim ex As RecogidaNoRecogidaException = PedidoVentaService.LeerNoRecogida(409, body)

        Assert.IsNotNull(ex)
        Assert.AreEqual("Incidentado", ex.Estado)
        Assert.AreEqual("estado Incidentado", ex.EstadoLegible)
        StringAssert.Contains(ex.Message, "no figura como recogida")
        Assert.IsNull(PedidoVentaService.LeerNoRecogida(400, body), "solo el 409")
        Assert.IsNull(PedidoVentaService.LeerNoRecogida(409, "{""Message"":""otro conflicto""}"), "otro 409")
        Assert.IsNull(PedidoVentaService.LeerNoRecogida(409, "no es json"))
    End Sub
#End Region

End Class
