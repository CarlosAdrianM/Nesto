Imports System.Threading.Tasks
Imports FakeItEasy
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.Modulos.Rapports
Imports Nesto.ViewModels

''' <summary>
''' Nesto#562 (NestoAPI#723): en la ficha comercial, la casilla «Exige P.O. en todos los pedidos» guarda al cambiarla
''' (PUT Clientes/{empresa}/{cliente}/SuPedidoObligatorio). Solo Administración, Dirección e Informática; si la API falla,
''' la casilla vuelve a como estaba y se enseña el motivo.
''' </summary>
<TestClass()>
Public Class ClientesViewModelSuPedidoObligatorioTests

    Private _configuracion As IConfiguracion
    Private _dialogService As IServicioDialogos
    Private _servicio As IClienteComercialService

    <TestInitialize()>
    Public Sub Initialize()
        _configuracion = A.Fake(Of IConfiguracion)()
        _dialogService = A.Fake(Of IServicioDialogos)()
        _servicio = A.Fake(Of IClienteComercialService)()
    End Sub

    Private Sub ElUsuarioEsDe(grupo As String)
        A.CallTo(Function() _configuracion.UsuarioEnGrupo(grupo)).Returns(True)
    End Sub

    Private Async Function CrearViewModelConFicha(Optional exige As Boolean = False) As Task(Of ClientesViewModel)
        Dim ficha As New ClienteJson With {.empresa = "1", .cliente = "15191", .contacto = "0", .nombre = "CHANEL", .suPedidoObligatorio = exige}
        A.CallTo(Function() _servicio.LeerCliente("1", "15191", "0")).Returns(Task.FromResult(ficha))
        Dim vm As New ClientesViewModel(_configuracion, _dialogService, _servicio, A.Fake(Of IRapportService)(), A.Fake(Of IServicioAutenticacion)())
        Await vm.ActualizarClienteAsync("1", "15191", "0")
        vm.clienteServidor = vm.clienteActivo
        Return vm
    End Function

    <TestMethod()>
    Public Async Function AlCargarLaFicha_LaCasillaEnsenaLaMarcaDelCliente() As Task
        Dim vm = Await CrearViewModelConFicha(exige:=True)

        Assert.IsTrue(vm.ExigeSuPedido)
    End Function

    <TestMethod()>
    Public Async Function Marcarla_ConPermiso_LlamaAlPutConExige() As Task
        ElUsuarioEsDe(Constantes.GruposSeguridad.ADMINISTRACION)
        Dim vm = Await CrearViewModelConFicha()

        Assert.IsTrue(vm.PuedeCambiarSuPedidoObligatorio)
        Await vm.CambiarSuPedidoObligatorioAsync(True)

        A.CallTo(Function() _servicio.PonerSuPedidoObligatorio("1", "15191", True)).MustHaveHappenedOnceExactly()
        Assert.IsTrue(vm.ExigeSuPedido)
        A.CallTo(Sub() _dialogService.ShowError(A(Of String).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function Desmarcarla_DesdeLaCasilla_LlamaAlPutConExigeFalse() As Task
        ElUsuarioEsDe(Constantes.GruposSeguridad.DIRECCION)
        Dim vm = Await CrearViewModelConFicha(exige:=True)

        vm.ExigeSuPedido = False ' lo que hace el binding de la casilla

        A.CallTo(Function() _servicio.PonerSuPedidoObligatorio("1", "15191", False)).MustHaveHappenedOnceExactly()
        Assert.IsFalse(vm.ExigeSuPedido)
    End Function

    <TestMethod()>
    Public Async Function SiElPutFalla_LaCasillaVuelveAComoEstabaYSeEnsenaElMotivo() As Task
        ElUsuarioEsDe(Constantes.GruposSeguridad.INFORMATICA)
        A.CallTo(Function() _servicio.PonerSuPedidoObligatorio(A(Of String).Ignored, A(Of String).Ignored, A(Of Boolean).Ignored)).
            ThrowsAsync(New Exception("Solo Administración, Dirección o Informática pueden cambiar si un cliente exige el P.O."))
        Dim vm = Await CrearViewModelConFicha()
        Dim avisos As New List(Of String)
        AddHandler vm.PropertyChanged, Sub(s, e) avisos.Add(e.PropertyName)

        Await vm.CambiarSuPedidoObligatorioAsync(True)

        Assert.IsFalse(vm.ExigeSuPedido, "Vuelve a desmarcada")
        Assert.IsFalse(vm.clienteServidor.suPedidoObligatorio)
        Assert.IsTrue(avisos.Where(Function(p) p = NameOf(ClientesViewModel.ExigeSuPedido)).Count() >= 2, "La casilla se entera de que vuelve atrás")
        A.CallTo(Sub() _dialogService.ShowError(A(Of String).That.Contains("Solo Administración"))).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function SinPermiso_EstaDeshabilitadaYNoLlamaALaApi() As Task
        ElUsuarioEsDe(Constantes.GruposSeguridad.ALMACEN)
        Dim vm = Await CrearViewModelConFicha()

        Assert.IsFalse(vm.PuedeCambiarSuPedidoObligatorio)
        StringAssert.Contains(vm.TooltipSuPedidoObligatorio, "Solo Administración")
        vm.ExigeSuPedido = True

        A.CallTo(Function() _servicio.PonerSuPedidoObligatorio(A(Of String).Ignored, A(Of String).Ignored, A(Of Boolean).Ignored)).MustNotHaveHappened()
        Assert.IsFalse(vm.ExigeSuPedido)
    End Function

    <TestMethod()>
    Public Sub SinFicha_NoHaceNada()
        ElUsuarioEsDe(Constantes.GruposSeguridad.ADMINISTRACION)
        Dim vm As New ClientesViewModel(_configuracion, _dialogService, _servicio, A.Fake(Of IRapportService)(), A.Fake(Of IServicioAutenticacion)())

        vm.ExigeSuPedido = True

        Assert.IsFalse(vm.ExigeSuPedido)
        A.CallTo(Function() _servicio.PonerSuPedidoObligatorio(A(Of String).Ignored, A(Of String).Ignored, A(Of Boolean).Ignored)).MustNotHaveHappened()
    End Sub

End Class
