Imports FakeItEasy
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models.Nesto.Models
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.ViewModels
Imports Prism.Regions
Imports Prism.Services.Dialogs
Imports System.Collections.ObjectModel
Imports System.Threading.Tasks

''' <summary>
''' Nesto#340 (Agencias, slice A4.2): «Recibir retorno» ya no hace un UPDATE por Entity Framework
''' desde el ViewModel; pide al servidor que estampe la fecha y refleja lo que este devuelve.
''' </summary>
<TestClass()>
Public Class AgenciasViewModelRetornosTests
    Private regionManager As IRegionManager
    Private servicio As IAgenciaService
    Private configuracion As IConfiguracion
    Private dialogService As IDialogService
    Private servicioPedidos As IPedidoVentaService
    Private servicioAutenticacion As IServicioAutenticacion
    Private viewModel As AgenciasViewModel
    Private retorno As EnviosAgencia
    Private otroRetorno As EnviosAgencia

    <TestInitialize()>
    Public Sub Initialize()
        configuracion = A.Fake(Of IConfiguracion)
        regionManager = A.Fake(Of RegionManager)
        servicio = A.Fake(Of IAgenciaService)
        dialogService = A.Fake(Of IDialogService)
        servicioPedidos = A.Fake(Of IPedidoVentaService)
        servicioAutenticacion = A.Fake(Of IServicioAutenticacion)
        viewModel = New AgenciasViewModel(regionManager, servicio, configuracion, dialogService, servicioPedidos, servicioAutenticacion)

        retorno = New EnviosAgencia With {.Numero = 248001, .Pedido = 925100, .Cliente = "29268     "}
        otroRetorno = New EnviosAgencia With {.Numero = 248002, .Pedido = 925101, .Cliente = "1         "}
        viewModel.listaRetornos = New ObservableCollection(Of EnviosAgencia) From {retorno, otroRetorno}
        viewModel.lineaRetornoSeleccionado = retorno
    End Sub

    ''' <summary>
    ''' ShowConfirmation es una extensión sobre ShowDialog: se simula que el usuario pulsa OK o
    ''' Cancelar. ShowError también pasa por ShowDialog pero sin callback (guard).
    ''' </summary>
    Private Sub ElUsuarioContesta(ok As Boolean)
        A.CallTo(Sub() dialogService.ShowDialog(
                    A(Of String).Ignored,
                    A(Of IDialogParameters).Ignored,
                    A(Of Action(Of IDialogResult)).Ignored)) _
         .Invokes(Sub(nombre As String, parametros As IDialogParameters, callback As Action(Of IDialogResult))
                      If callback Is Nothing Then
                          Return
                      End If
                      Dim resultado = A.Fake(Of IDialogResult)
                      A.CallTo(Function() resultado.Result).Returns(If(ok, ButtonResult.OK, ButtonResult.Cancel))
                      callback(resultado)
                  End Sub)
    End Sub

    <TestMethod()>
    Public Async Function RecibirRetorno_Confirmado_PideLaFechaAlServidorYQuitaElRetornoDeLaLista() As Task
        ElUsuarioContesta(True)
        A.CallTo(Function() servicio.RecibirRetorno(248001)).Returns(Task.FromResult(New Date(2026, 9, 18)))

        Await viewModel.RecibirRetornoSeleccionado()

        A.CallTo(Function() servicio.RecibirRetorno(248001)).MustHaveHappenedOnceExactly()
        ' .Value: con Date frente a Date? VB (Option Strict Off) elige otra sobrecarga de AreEqual y
        ' el mensaje intenta convertirse a Boolean.
        Assert.AreEqual(New Date(2026, 9, 18), retorno.FechaRetornoRecibido.Value, "la fecha es la que grabó el servidor, no Today del cliente")
        Assert.AreEqual(1, viewModel.listaRetornos.Count)
        Assert.IsFalse(viewModel.listaRetornos.Contains(retorno))
        Assert.IsTrue(viewModel.listaRetornos.Contains(otroRetorno))
        StringAssert.Contains(viewModel.mensajeError, "29268")
        StringAssert.Contains(viewModel.mensajeError, "actualizada correctamente")
    End Function

    <TestMethod()>
    Public Async Function RecibirRetorno_Cancelado_NoLlamaAlServidorNiTocaLaLista() As Task
        ElUsuarioContesta(False)

        Await viewModel.RecibirRetornoSeleccionado()

        A.CallTo(Function() servicio.RecibirRetorno(A(Of Integer).Ignored)).MustNotHaveHappened()
        Assert.AreEqual(2, viewModel.listaRetornos.Count)
        Assert.IsNull(retorno.FechaRetornoRecibido)
    End Function

    <TestMethod()>
    Public Async Function RecibirRetorno_ElServidorRechaza_ElRetornoSigueEnLaListaYSeEnsenaElMotivo() As Task
        ' P. ej. otra sesión ya lo había recibido: el servidor contesta 400 con la fecha y aquí no
        ' se quita nada de la lista, que la fila sigue sin fecha en esta pantalla.
        ElUsuarioContesta(True)
        A.CallTo(Function() servicio.RecibirRetorno(248001)).Throws(New Exception("NestoAPI rechazó la recepción del retorno (400): ya se recibió el 17/09/2026."))

        Await viewModel.RecibirRetornoSeleccionado()

        Assert.AreEqual(2, viewModel.listaRetornos.Count)
        Assert.IsNull(retorno.FechaRetornoRecibido)
        StringAssert.Contains(viewModel.mensajeError, "error")
        ' ShowError pasa por ShowDialog (sin callback): además de la confirmación, hubo un aviso.
        A.CallTo(Sub() dialogService.ShowDialog(A(Of String).Ignored, A(Of IDialogParameters).Ignored, A(Of Action(Of IDialogResult)).Ignored)).MustHaveHappenedTwiceExactly()
    End Function

    <TestMethod()>
    Public Async Function RecibirRetorno_SinLineaSeleccionada_NoPreguntaNiLlama() As Task
        viewModel.lineaRetornoSeleccionado = Nothing

        Await viewModel.RecibirRetornoSeleccionado()

        A.CallTo(Function() servicio.RecibirRetorno(A(Of Integer).Ignored)).MustNotHaveHappened()
        A.CallTo(Sub() dialogService.ShowDialog(A(Of String).Ignored, A(Of IDialogParameters).Ignored, A(Of Action(Of IDialogResult)).Ignored)).MustNotHaveHappened()
        Assert.AreEqual("No hay ninguna línea seleccionada", viewModel.mensajeError)
    End Function

End Class
