Imports FakeItEasy
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models.Nesto.Models
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.ViewModels
Imports System.Collections.ObjectModel

''' <summary>
''' ELMAH 12/11/26 10:17 (Nesto 1.10.60.1, pantalla Agencias): NullReferenceException en el setter de
''' agenciaSeleccionada, en la lambda de listaPaises.Single. Una agencia sin clase en factory (históricos de
''' Sending, OnTime, Glovo...) deja agenciaEspecifica = Nothing (Nesto#467) y en las pestañas Pedidos, En curso
''' y Etiquetas se intentaban poner igualmente sus defectos.
''' </summary>
<TestClass()>
Public Class AgenciasViewModelAgenciaSinClaseTests
    Private Const ASM As Integer = 1
    Private Const CTT As Integer = 13
    Private Const SENDING As Integer = 4

    Private servicio As IAgenciaService
    Private configuracion As IConfiguracion
    Private dialogService As IServicioDialogos
    Private viewModel As AgenciasViewModel

    <TestInitialize()>
    Public Sub Initialize()
        servicio = A.Fake(Of IAgenciaService)
        configuracion = A.Fake(Of IConfiguracion)
        dialogService = A.Fake(Of IServicioDialogos)

        A.CallTo(Function() configuracion.leerParametro("1", "EmpresaPorDefecto")).Returns("1  ")
        A.CallTo(Function() configuracion.leerParametro(A(Of String).Ignored, "UltNumPedidoVta")).Returns("1")

        Dim empresa = A.Fake(Of Empresas)
        empresa.Número = "1  "
        A.CallTo(Function() servicio.CargarListaEmpresas()).Returns(New ObservableCollection(Of Empresas) From {empresa})
        A.CallTo(Function() servicio.CargarListaAgencias(A(Of String).Ignored)).ReturnsLazily(
            Function() New ObservableCollection(Of AgenciasTransporte) From {Agencia(ASM, "ASM"), Agencia(CTT, "CTT"), Agencia(SENDING, "Sending")})
        A.CallTo(Function() servicio.CargarListaEnvios(A(Of Integer).Ignored)).ReturnsLazily(
            Function() New ObservableCollection(Of EnviosAgencia))
        A.CallTo(Function() servicio.CargarListaEnviosPedido(A(Of String).Ignored, A(Of Integer).Ignored)).ReturnsLazily(
            Function() New ObservableCollection(Of EnviosAgencia))
    End Sub

    Private Shared Function Agencia(numero As Integer, nombre As String) As AgenciasTransporte
        Return New AgenciasTransporte With {.Empresa = "1  ", .Numero = numero, .Nombre = nombre}
    End Function

    Private Sub AbrirVentana(pestanna As String)
        viewModel = New AgenciasViewModel(servicio, configuracion, dialogService,
                                          A.Fake(Of IPedidoVentaService), A.Fake(Of IServicioAutenticacion))
        viewModel.PestannaNombre = Pestannas.PEDIDOS
        viewModel.cmdCargarDatos.Execute(Nothing)
        viewModel.PestannaNombre = pestanna
        Fake.ClearRecordedCalls(dialogService)
    End Sub

    Private Sub SeleccionarAgenciaSinClase()
        ' Como al abrir un envío antiguo: la agencia llega de CargarAgencia, no del desplegable.
        viewModel.agenciaSeleccionada = Agencia(SENDING, "Sending")
    End Sub

    Private Sub ComprobarQueNoHayError()
        A.CallTo(Sub() dialogService.ShowError(A(Of String).Ignored)).MustNotHaveHappened()
        Assert.AreEqual(SENDING, viewModel.agenciaSeleccionada.Numero)
        Assert.IsNull(viewModel.AgenciaEfectiva(0).Agencia, "Sin clase, agenciaEspecifica queda Nothing (solo consulta)")
    End Sub

    <TestMethod()>
    Public Sub AgenciaSinClase_EnCurso_NoLanzaNiRegistraError()
        AbrirVentana(Pestannas.EN_CURSO)
        SeleccionarAgenciaSinClase()
        ComprobarQueNoHayError()
        A.CallTo(Function() servicio.CargarListaEnvios(SENDING)).MustHaveHappened()
    End Sub

    <TestMethod()>
    Public Sub AgenciaSinClase_Pedidos_NoLanzaNiRegistraError()
        AbrirVentana(Pestannas.PEDIDOS)
        SeleccionarAgenciaSinClase()
        ComprobarQueNoHayError()
    End Sub

    <TestMethod()>
    Public Sub AgenciaSinClase_Etiquetas_NoLanzaNiRegistraError()
        AbrirVentana(Pestannas.ETIQUETAS)
        SeleccionarAgenciaSinClase()
        ComprobarQueNoHayError()
    End Sub

    <TestMethod()>
    Public Sub AgenciaConClase_EnCurso_SiguePoniendoSusDefectos()
        AbrirVentana(Pestannas.EN_CURSO)
        SeleccionarAgenciaSinClase()
        viewModel.agenciaSeleccionada = viewModel.listaAgencias.Single(Function(a) a.Numero = CTT)

        A.CallTo(Sub() dialogService.ShowError(A(Of String).Ignored)).MustNotHaveHappened()
        Dim agenciaCtt = New AgenciaCTT()
        Assert.IsInstanceOfType(viewModel.AgenciaEfectiva(0).Agencia, GetType(AgenciaCTT))
        Assert.AreEqual(agenciaCtt.paisDefecto, viewModel.paisActual.Id)
        Assert.AreEqual(agenciaCtt.retornoSinRetorno, viewModel.retornoActual.id)
        Assert.AreEqual(agenciaCtt.ServicioDefecto, viewModel.servicioActual.ServicioId)
        Assert.AreEqual(agenciaCtt.HorarioDefecto, viewModel.horarioActual.id)
    End Sub

End Class
