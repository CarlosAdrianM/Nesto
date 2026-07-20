Imports System
Imports System.Collections.Generic
Imports System.ComponentModel.DataAnnotations
Imports System.Linq
Imports System.Threading.Tasks
Imports ControlesUsuario.Dialogs
Imports FakeItEasy
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Models
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.Modulos.PedidoVenta.PedidoVentaModel
Imports Nesto.Modulos.PedidoVenta.ViewModels
Imports Prism.Events
Imports Prism.Regions
Imports Prism.Services.Dialogs

''' <summary>
''' Nesto#416: unir pedidos desde la lista.
''' Antes el setter llamaba al servicio SIN Await, así que el Catch no podía saltar y el mensaje
''' "se han unido correctamente" salía SIEMPRE, aunque el servidor rechazara la unión. Y si el
''' rechazo era por validación de precios/descuentos, no se ofrecía "¿de todos modos?" (sí se
''' ofrece al crear un pedido).
'''
''' Los diálogos son métodos de EXTENSIÓN (estáticos, no fakeables): se configura el ShowDialog
''' real de IDialogService, que es al que acaban llamando todos.
''' </summary>
<TestClass()>
Public Class ListaPedidosUnirTests

    Private _servicio As IPedidoVentaService
    Private _dialogService As IDialogService
    Private _configuracion As IConfiguracion
    Private _dialogosMostrados As List(Of Tuple(Of String, String))

    <TestInitialize()>
    Public Sub Initialize()
        _servicio = A.Fake(Of IPedidoVentaService)()
        _dialogService = A.Fake(Of IDialogService)()
        _configuracion = A.Fake(Of IConfiguracion)()
        _dialogosMostrados = New List(Of Tuple(Of String, String))
    End Sub

    ''' <summary>Responde a las confirmaciones con el botón indicado y registra todos los diálogos.</summary>
    Private Sub ResponderConfirmaciones(resultado As ButtonResult)
        Dim res = New DialogResult(resultado)
        A.CallTo(Sub() _dialogService.ShowDialog(A(Of String).Ignored, A(Of IDialogParameters).Ignored, A(Of Action(Of IDialogResult)).Ignored)) _
            .Invokes(Of String, IDialogParameters, Action(Of IDialogResult))(
                Sub(nombre, parametros, callback)
                    _dialogosMostrados.Add(Tuple.Create(nombre, parametros.GetValue(Of String)("message")))
                    If callback IsNot Nothing Then
                        callback(res)
                    End If
                End Sub)
    End Sub

    Private Function HayMensajeQueContenga(texto As String) As Boolean
        Return _dialogosMostrados.Any(Function(d) d.Item2 IsNot Nothing AndAlso d.Item2.Contains(texto))
    End Function

    Private Function CrearViewModel() As ListaPedidosVentaViewModel
        Dim vm = New ListaPedidosVentaViewModel(_configuracion, _servicio, A.Fake(Of IEventAggregator)(), _dialogService, A.Fake(Of IRegionManager)())
        vm.ListaPedidos.ElementoSeleccionado = New ResumenPedido With {.empresa = "1", .numero = 200}
        Return vm
    End Function

    <TestMethod()>
    Public Async Function Unir_SiElServidorFalla_MuestraErrorYNoDiceQueSeUnioCorrectamente() As Task
        ' El bug gordo: sin Await, el "correctamente" salía aunque la unión hubiera fallado
        ResponderConfirmaciones(ButtonResult.OK)
        A.CallTo(Function() _servicio.UnirPedidos(A(Of String).Ignored, A(Of Integer).Ignored, A(Of Integer).Ignored, A(Of Boolean).Ignored)) _
            .Throws(New Exception("No se pudo unir"))
        Dim vm = CrearViewModel()

        Await vm.UnirPedidoSeleccionadoAsync(New ResumenPedido With {.empresa = "1", .numero = 100})

        Assert.IsTrue(HayMensajeQueContenga("No se pudo unir"), "Debe mostrar el error real del servidor")
        Assert.IsFalse(HayMensajeQueContenga("correctamente"), "NO debe decir que se unieron correctamente")
    End Function

    <TestMethod()>
    Public Async Function Unir_ErrorDeValidacion_PreguntaYReintentaSinPasarValidacion() As Task
        ResponderConfirmaciones(ButtonResult.OK)
        A.CallTo(Function() _servicio.UnirPedidos(A(Of String).Ignored, A(Of Integer).Ignored, A(Of Integer).Ignored, False)) _
            .Throws(New ValidationException("No se encuentra autorizado el descuento del 50 %"))
        A.CallTo(Function() _servicio.UnirPedidos(A(Of String).Ignored, A(Of Integer).Ignored, A(Of Integer).Ignored, True)) _
            .Returns(Task.FromResult(New PedidoVentaDTO()))
        Dim vm = CrearViewModel()

        Await vm.UnirPedidoSeleccionadoAsync(New ResumenPedido With {.empresa = "1", .numero = 100})

        Assert.IsTrue(HayMensajeQueContenga("de todos modos"), "Debe preguntar si unir de todos modos")
        A.CallTo(Function() _servicio.UnirPedidos(A(Of String).Ignored, A(Of Integer).Ignored, A(Of Integer).Ignored, True)) _
            .MustHaveHappenedOnceExactly()
        Assert.IsTrue(HayMensajeQueContenga("correctamente"), "Tras el reintento debe confirmar el éxito")
    End Function

    <TestMethod()>
    Public Async Function Unir_ErrorDeValidacion_SiElUsuarioDiceQueNo_NoReintentaYMuestraElError() As Task
        ResponderConfirmaciones(ButtonResult.Cancel)
        A.CallTo(Function() _servicio.UnirPedidos(A(Of String).Ignored, A(Of Integer).Ignored, A(Of Integer).Ignored, A(Of Boolean).Ignored)) _
            .Throws(New ValidationException("No se encuentra autorizado el descuento del 50 %"))
        Dim vm = CrearViewModel()

        Await vm.UnirPedidoSeleccionadoAsync(New ResumenPedido With {.empresa = "1", .numero = 100})

        A.CallTo(Function() _servicio.UnirPedidos(A(Of String).Ignored, A(Of Integer).Ignored, A(Of Integer).Ignored, True)) _
            .MustNotHaveHappened()
        Assert.IsFalse(HayMensajeQueContenga("correctamente"), "Si el usuario no confirma, no se une nada")
    End Function
End Class
