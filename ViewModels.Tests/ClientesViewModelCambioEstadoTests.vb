Imports System.Threading.Tasks
Imports ControlesUsuario.Dialogs
Imports FakeItEasy
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.Modulos.Rapports
Imports Nesto.ViewModels

''' <summary>
''' Nesto#558 (NestoAPI#716, 2.2): en la ficha comercial, al guardar con el estado cambiado a una baja (9, -1, 93, 11,
''' 22, 95, 99) o saliendo de ella, se pregunta el motivo y el comentario (opcionales) y viajan en el PUT de
''' ClienteComercial (motivoCambioEstado / comentarioCambioEstado). Si el estado no cambia, ni se pregunta ni se manda.
''' </summary>
<TestClass()>
Public Class ClientesViewModelCambioEstadoTests

    Private _configuracion As IConfiguracion
    Private _dialogService As IServicioDialogos
    Private _servicio As IClienteComercialService
    Private _servicioRapports As IRapportService
    Private _servicioAutenticacion As IServicioAutenticacion

    <TestInitialize()>
    Public Sub Initialize()
        _configuracion = A.Fake(Of IConfiguracion)()
        _dialogService = A.Fake(Of IServicioDialogos)()
        _servicio = A.Fake(Of IClienteComercialService)()
        _servicioRapports = A.Fake(Of IRapportService)()
        _servicioAutenticacion = A.Fake(Of IServicioAutenticacion)()
    End Sub

    Private Sub ElDialogoContesta(respuesta As ResultadoDialogo)
        A.CallTo(Sub() _dialogService.ShowDialog(MotivoCambioEstadoDialogViewModel.NOMBRE, A(Of ParametrosDialogo).Ignored, A(Of Action(Of ResultadoDialogo)).Ignored)) _
            .Invokes(Sub(nombre As String, parametros As ParametrosDialogo, callback As Action(Of ResultadoDialogo)) callback(respuesta))
    End Sub

    ''' <summary>La ficha cargada en estado <paramref name="estadoInicial"/>, con clienteServidor = clienteActivo como en la pantalla.</summary>
    Private Async Function CrearViewModelConFicha(estadoInicial As Integer) As Task(Of ClientesViewModel)
        Dim ficha As New ClienteJson With {.empresa = "1", .cliente = "15191", .contacto = "0", .nombre = "EL EDÉN", .estado = estadoInicial}
        A.CallTo(Function() _servicio.LeerCliente("1", "15191", "0")).Returns(Task.FromResult(ficha))
        Dim vm As New ClientesViewModel(_configuracion, _dialogService, _servicio, _servicioRapports, _servicioAutenticacion)
        Await vm.ActualizarClienteAsync("1", "15191", "0")
        vm.clienteServidor = vm.clienteActivo
        Return vm
    End Function

    <TestMethod()>
    Public Async Function AlCargarLaFicha_SeRecuerdaElEstadoDePartida() As Task
        Dim vm = Await CrearViewModelConFicha(0)

        Assert.AreEqual(0, vm.EstadoCargado.Value)
        vm.clienteActivo.estado = -1 ' el TextBox edita el mismo objeto
        Assert.AreEqual(0, vm.EstadoCargado.Value, "Editar el estado no cambia el de partida hasta que se guarda")
    End Function

    <TestMethod()>
    Public Async Function EstadoABaja_PreguntaElMotivo_YLoDejaEnLaFichaParaElPut() As Task
        ElDialogoContesta(New ResultadoDialogo(ResultadoBoton.OK, New ParametrosDialogo From {{"motivo", MotivoCambioEstadoCliente.CIERRE}, {"comentario", "Cerró en septiembre"}}))
        Dim vm = Await CrearViewModelConFicha(0)
        vm.clienteActivo.estado = -1

        Assert.IsTrue(vm.PedirMotivoCambioEstadoSiHaceFalta())

        Assert.AreEqual("Cierre", vm.clienteServidor.motivoCambioEstado)
        Assert.AreEqual("Cerró en septiembre", vm.clienteServidor.comentarioCambioEstado)
        A.CallTo(Sub() _dialogService.ShowDialog(MotivoCambioEstadoDialogViewModel.NOMBRE, A(Of ParametrosDialogo).Ignored, A(Of Action(Of ResultadoDialogo)).Ignored)).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function EstadoABaja_SinMotivo_NoBloqueaYNoMandaNada() As Task
        ElDialogoContesta(New ResultadoDialogo(ResultadoBoton.Cancel))
        Dim vm = Await CrearViewModelConFicha(0)
        vm.clienteActivo.estado = 93

        Assert.IsTrue(vm.PedirMotivoCambioEstadoSiHaceFalta(), "Se ha preguntado")

        Assert.IsNull(vm.clienteServidor.motivoCambioEstado, "Viaja null: la API sigue como hasta ahora")
        Assert.IsNull(vm.clienteServidor.comentarioCambioEstado)
    End Function

    <TestMethod()>
    Public Async Function EstadoSinCambiar_NoPreguntaNiManda() As Task
        ElDialogoContesta(New ResultadoDialogo(ResultadoBoton.OK, New ParametrosDialogo From {{"motivo", "Cierre"}}))
        Dim vm = Await CrearViewModelConFicha(-1)
        vm.clienteServidor.motivoCambioEstado = "basura de un intento anterior"

        Assert.IsFalse(vm.PedirMotivoCambioEstadoSiHaceFalta())

        Assert.IsNull(vm.clienteServidor.motivoCambioEstado, "Se limpia aunque no se pregunte")
        A.CallTo(Sub() _dialogService.ShowDialog(A(Of String).Ignored, A(Of ParametrosDialogo).Ignored, A(Of Action(Of ResultadoDialogo)).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function EstadoAOtroQueNoEsBaja_NoPregunta() As Task
        ElDialogoContesta(New ResultadoDialogo(ResultadoBoton.OK, New ParametrosDialogo From {{"motivo", "Cierre"}}))
        Dim vm = Await CrearViewModelConFicha(0)
        vm.clienteActivo.estado = Constantes.Clientes.ESTADO_NO_VISITABLE

        Assert.IsFalse(vm.PedirMotivoCambioEstadoSiHaceFalta())

        Assert.IsNull(vm.clienteServidor.motivoCambioEstado)
        A.CallTo(Sub() _dialogService.ShowDialog(A(Of String).Ignored, A(Of ParametrosDialogo).Ignored, A(Of Action(Of ResultadoDialogo)).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function SalirDeUnaBaja_PreguntaElMotivo_Reactivacion() As Task
        ElDialogoContesta(New ResultadoDialogo(ResultadoBoton.OK, New ParametrosDialogo From {{"motivo", MotivoCambioEstadoCliente.REACTIVACION}, {"comentario", Nothing}}))
        Dim vm = Await CrearViewModelConFicha(-1)
        vm.clienteActivo.estado = 0

        Assert.IsTrue(vm.PedirMotivoCambioEstadoSiHaceFalta())

        Assert.AreEqual("Reactivacion", vm.clienteServidor.motivoCambioEstado)
        Assert.IsNull(vm.clienteServidor.comentarioCambioEstado)
    End Function

    <TestMethod()>
    Public Async Function SinFichaCargada_NoPreguntaNiRompe() As Task
        Dim vm As New ClientesViewModel(_configuracion, _dialogService, _servicio, _servicioRapports, _servicioAutenticacion)

        Assert.IsFalse(vm.PedirMotivoCambioEstadoSiHaceFalta())
    End Function

    <TestMethod()>
    Public Sub ClienteJson_LlevaLosCamposDelPut_YNullSiNoSeEligen()
        Dim ficha As New ClienteJson With {.empresa = "1", .cliente = "15191", .estado = -1}

        Dim sinMotivo = Newtonsoft.Json.Linq.JObject.FromObject(ficha)
        Assert.AreEqual(Newtonsoft.Json.Linq.JTokenType.Null, sinMotivo("motivoCambioEstado").Type)
        Assert.AreEqual(Newtonsoft.Json.Linq.JTokenType.Null, sinMotivo("comentarioCambioEstado").Type)

        ficha.motivoCambioEstado = MotivoCambioEstadoCliente.IMPAGO
        ficha.comentarioCambioEstado = "Dos facturas sin pagar"
        Dim conMotivo = Newtonsoft.Json.Linq.JObject.FromObject(ficha)
        Assert.AreEqual("Impago", conMotivo("motivoCambioEstado").ToString())
        Assert.AreEqual("Dos facturas sin pagar", conMotivo("comentarioCambioEstado").ToString())
    End Sub
End Class
