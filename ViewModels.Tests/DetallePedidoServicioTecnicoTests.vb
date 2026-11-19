Imports FakeItEasy
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models
Imports Nesto.Modulos.PedidoVenta
Imports CommunityToolkit.Mvvm.Messaging
Imports System.Collections.ObjectModel
Imports System.Threading.Tasks
Imports ControlesUsuario.Dialogs

''' <summary>
''' NestoAPI#881 (corte 2): el «Servicio técnico» del panel de la nota de entrega o de recogida. Combo editable con los tipos de la API y
''' minutos opcionales; en la nota nueva viaja con el POST, en la grabada va al momento por PUT …/ServicioTecnico.
''' </summary>
<TestClass()>
Public Class DetallePedidoServicioTecnicoTests

    Private servicio As IPedidoVentaService
    Private dialogService As IServicioDialogos
    Private configuracion As IConfiguracion

    <TestInitialize()>
    Public Sub Initialize()
        servicio = A.Fake(Of IPedidoVentaService)
        dialogService = A.Fake(Of IServicioDialogos)
        configuracion = A.Fake(Of IConfiguracion)
        A.CallTo(Function() servicio.LeerTiposServicioTecnico()).Returns(Task.FromResult(New List(Of TipoServicioTecnicoDTO) From {
            New TipoServicioTecnicoDTO With {.Id = 1, .Nombre = "Instalar sillón", .MinutosEstimados = 90, .Activo = True},
            New TipoServicioTecnicoDTO With {.Id = 2, .Nombre = "Revisión", .Activo = True}}))
        A.CallTo(Function() servicio.LeerServicioTecnicoNota(A(Of String).Ignored, A(Of Integer).Ignored)).
            Returns(Task.FromResult(New ServicioTecnicoNotaDTO With {.Empresa = "1", .Pedido = 927519}))
    End Sub

    Private Function CrearVm(Optional almacen As Boolean = True) As DetallePedidoViewModel
        If almacen Then
            A.CallTo(Function() configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN)).Returns(True)
        End If
        Return New DetallePedidoViewModel(A.Fake(Of IServicioNavegacion), configuracion, servicio, New WeakReferenceMessenger(),
                                          dialogService, A.Fake(Of IAbridorPedidos), A.Fake(Of IServicioAutenticacion))
    End Function

    Private Shared Function Nota(numero As Integer) As PedidoVentaWrapper
        Return New PedidoVentaWrapper(New PedidoVentaDTO With {
            .empresa = "1", .numero = numero, .cliente = "15191", .contacto = "0", .notaEntrega = True, .tipoNotaEntrega = "Recogida", .bultosNota = 1,
            .Lineas = New ObservableCollection(Of LineaPedidoVentaDTO)})
    End Function

    <TestMethod()>
    Public Async Function NotaGrabada_AlCargar_LeeSuServicioTecnico_YLosTiposDelCombo() As Task
        A.CallTo(Function() servicio.LeerServicioTecnicoNota("1", 927519)).Returns(Task.FromResult(New ServicioTecnicoNotaDTO With {
            .Empresa = "1", .Pedido = 927519, .TipoServicioTecnicoId = 1, .ServicioTecnico = "Instalar sillón", .MinutosEstimados = 90}))
        Dim vm = CrearVm()

        vm.pedido = Nota(927519)
        Await vm.CargaTiposServicioTecnicoEnCurso
        Await vm.CargaServicioTecnicoEnCurso

        Assert.IsTrue(vm.PuedeIndicarServicioTecnico)
        CollectionAssert.AreEqual({"Instalar sillón", "Revisión"}, vm.TiposServicioTecnico.ToList())
        Assert.AreEqual("Instalar sillón", vm.ServicioTecnico)
        Assert.IsNull(vm.MinutosServicioTexto)
        StringAssert.Contains(vm.TextoMinutosServicio, "90")
        A.CallTo(Function() servicio.CambiarServicioTecnicoNota(A(Of String).Ignored, A(Of Integer).Ignored, A(Of String).Ignored, A(Of Integer?).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function NotaGrabada_CambiarTipoYMinutos_VaPorElPut() As Task
        A.CallTo(Function() servicio.CambiarServicioTecnicoNota(A(Of String).Ignored, A(Of Integer).Ignored, A(Of String).Ignored, A(Of Integer?).Ignored)).
            ReturnsLazily(Function(e As String, p As Integer, t As String, m As Integer?) Task.FromResult(New ServicioTecnicoNotaDTO With {
                .Empresa = e, .Pedido = p, .ServicioTecnico = t, .MinutosServicio = m, .MinutosEstimados = If(m, 60)}))
        Dim vm = CrearVm()
        vm.pedido = Nota(927519)
        Await vm.CargaServicioTecnicoEnCurso

        vm.ServicioTecnico = "  Montar   mesa "
        Await vm.CambioServicioTecnicoEnCurso
        vm.MinutosServicioTexto = "40"
        Await vm.CambioServicioTecnicoEnCurso

        A.CallTo(Function() servicio.CambiarServicioTecnicoNota("1", 927519, "Montar mesa", Nothing)).MustHaveHappenedOnceExactly()
        A.CallTo(Function() servicio.CambiarServicioTecnicoNota("1", 927519, "Montar mesa", 40)).MustHaveHappenedOnceExactly()
        Assert.AreEqual("40", vm.MinutosServicioTexto)
        Assert.IsTrue(vm.TiposServicioTecnico.Contains("Montar mesa"), "El nuevo entra en el combo")
    End Function

    <TestMethod()>
    Public Async Function NotaGrabada_SiLaApiLoRechaza_EnsenaSuMensajeYVuelveAtras() As Task
        A.CallTo(Function() servicio.CambiarServicioTecnicoNota(A(Of String).Ignored, A(Of Integer).Ignored, A(Of String).Ignored, A(Of Integer?).Ignored)).
            ThrowsAsync(New Exception("La nota 927519 ya está entregada: no se puede cambiar su servicio técnico."))
        Dim vm = CrearVm()
        vm.pedido = Nota(927519)
        Await vm.CargaServicioTecnicoEnCurso

        vm.ServicioTecnico = "Revisión"
        Await vm.CambioServicioTecnicoEnCurso

        A.CallTo(Sub() dialogService.ShowError("La nota 927519 ya está entregada: no se puede cambiar su servicio técnico.")).MustHaveHappenedOnceExactly()
        Assert.IsNull(vm.ServicioTecnico)
    End Function

    <TestMethod()>
    Public Sub MinutosQueNoValen_NoSeGuardan_YSeDice()
        Dim vm = CrearVm()
        vm.pedido = Nota(927519)
        vm.MinutosServicioTexto = "mucho"
        vm.MinutosServicioTexto = "601"

        A.CallTo(Sub() dialogService.ShowError(DetallePedidoViewModel.MENSAJE_MINUTOS_SERVICIO)).MustHaveHappenedTwiceExactly()
        Assert.IsNull(vm.MinutosServicioTexto)
        A.CallTo(Function() servicio.CambiarServicioTecnicoNota(A(Of String).Ignored, A(Of Integer).Ignored, A(Of String).Ignored, A(Of Integer?).Ignored)).MustNotHaveHappened()
    End Sub

    <TestMethod()>
    Public Sub NotaNueva_ViajaConElPost_SinLlamarALaApi()
        Dim vm = CrearVm()
        vm.pedido = Nota(0)

        vm.ServicioTecnico = "Instalar sillón"
        vm.MinutosServicioTexto = "75"

        Assert.AreEqual("Instalar sillón", vm.pedido.Model.tipoServicioTecnicoNombre)
        Assert.AreEqual(75, vm.pedido.Model.minutosServicio)
        A.CallTo(Function() servicio.CambiarServicioTecnicoNota(A(Of String).Ignored, A(Of Integer).Ignored, A(Of String).Ignored, A(Of Integer?).Ignored)).MustNotHaveHappened()

        vm.ServicioTecnico = " "
        Assert.IsNull(vm.pedido.Model.tipoServicioTecnicoNombre, "Vacío: no lleva")
        Assert.IsNull(vm.pedido.Model.minutosServicio)
    End Sub

    <TestMethod()>
    Public Sub SinGrupoDeNotas_NiSeVeNiSeLee()
        Dim vm = CrearVm(almacen:=False)
        vm.pedido = Nota(927519)

        Assert.IsFalse(vm.PuedeIndicarServicioTecnico)
        A.CallTo(Function() servicio.LeerServicioTecnicoNota(A(Of String).Ignored, A(Of Integer).Ignored)).MustNotHaveHappened()
        A.CallTo(Function() servicio.LeerTiposServicioTecnico()).MustNotHaveHappened()
    End Sub
End Class
