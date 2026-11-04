Imports System.Net
Imports System.Net.Http
Imports System.Threading
Imports System.Threading.Tasks
Imports ControlesUsuario.Services
Imports FakeItEasy
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Modulos.Rapports
Imports Nesto.ViewModels
Imports Newtonsoft.Json.Linq

' Nesto#611 (Reina, 03/11/26: cinco veces «Indique un concepto…»): en la ficha del cliente, el enlace de pago sin efectos
' que liquidar pide el concepto en pantalla ANTES de llamar a la API; y lo que la API rechaza por validación se enseña
' tal cual, sin el prefijo técnico y sin registrarlo en ELMAH.
<TestClass()>
Public Class ClientesViewModelConceptoEnlacePagoTests

    Private Class ManejadorFalso
        Inherits HttpMessageHandler

        Public ReadOnly Cuerpos As New List(Of String)
        Public Property Respuesta As Func(Of HttpResponseMessage) =
            Function() New HttpResponseMessage(HttpStatusCode.OK) With {
                .Content = New StringContent("{""UrlPaginaPago"":""https://pago.nuevavision.es/abc""}")
            }

        Protected Overrides Async Function SendAsync(request As HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
            Cuerpos.Add(If(request.Content Is Nothing, Nothing, Await request.Content.ReadAsStringAsync()))
            Return Respuesta.Invoke()
        End Function
    End Class

    Private _dialogos As IServicioDialogos
    Private _errores As IServicioRegistroErrores
    Private _manejador As ManejadorFalso
    Private _factoria As IClienteApiFactory
    Private _vm As ClientesViewModel

    <TestInitialize()>
    Public Sub Initialize()
        _dialogos = A.Fake(Of IServicioDialogos)()
        _errores = A.Fake(Of IServicioRegistroErrores)()
        _manejador = New ManejadorFalso()
        _factoria = A.Fake(Of IClienteApiFactory)()
        A.CallTo(Function() _factoria.Crear()).ReturnsLazily(Function() New HttpClient(_manejador, False) With {
            .BaseAddress = New Uri("http://localhost/api/")
        })
        Dim autenticacion = A.Fake(Of IServicioAutenticacion)()
        A.CallTo(Function() autenticacion.ConfigurarAutorizacion(A(Of HttpClient).Ignored)).Returns(Task.FromResult(True))
        Dim revisor = A.Fake(Of IRevisorConceptoPago)()
        A.CallTo(Function() revisor.ElegirConcepto(A(Of String).Ignored, A(Of String).Ignored, A(Of String).Ignored)) _
            .ReturnsLazily(Function(concepto As String, empresa As String, cliente As String) Task.FromResult(concepto))
        _vm = New ClientesViewModel(A.Fake(Of IConfiguracion)(), _dialogos, A.Fake(Of IClienteComercialService)(),
                                    A.Fake(Of IRapportService)(), autenticacion) With {
            .ClienteApiFactory = _factoria,
            .RegistroErrores = _errores,
            .RevisorConceptoPago = revisor,
            .MotorPagosSeleccionado = "NestoPago",
            .ImporteReclamarDeuda = 25D,
            .CorreoReclamarDeuda = "cliente@example.com"
        }
    End Sub

    <TestMethod()>
    Public Async Function ReclamarDeuda_SinEfectosNiConcepto_YNoSeEscribe_NoLlamaALaApi() As Task
        _vm.AsuntoReclamarDeuda = String.Empty
        A.CallTo(Function() _dialogos.GetText(A(Of String).Ignored, A(Of String).Ignored)).Returns(Nothing)

        Await _vm.ReclamarDeudaAsync()

        A.CallTo(Function() _dialogos.GetText(ClientesViewModel.TITULO_PEDIR_CONCEPTO_PAGO, ClientesViewModel.TEXTO_PEDIR_CONCEPTO_PAGO)) _
            .MustHaveHappenedOnceExactly()
        A.CallTo(Function() _factoria.Crear()).MustNotHaveHappened()
        Assert.AreEqual(0, _manejador.Cuerpos.Count)
        A.CallTo(Sub() _dialogos.ShowError(A(Of String).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function ReclamarDeuda_SinEfectosConElAsuntoPorDefecto_TambienPideElConcepto() As Task
        _vm.AsuntoReclamarDeuda = ClientesViewModel.ASUNTO_PAGO_POR_DEFECTO
        A.CallTo(Function() _dialogos.GetText(A(Of String).Ignored, A(Of String).Ignored)).Returns("   ")

        Await _vm.ReclamarDeudaAsync()

        A.CallTo(Function() _factoria.Crear()).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function ReclamarDeuda_SinEfectos_ConElConceptoEscritoEnElCuadro_LlamaConEl() As Task
        _vm.AsuntoReclamarDeuda = String.Empty
        A.CallTo(Function() _dialogos.GetText(A(Of String).Ignored, A(Of String).Ignored)).Returns(" Pago pedido 123456 ")

        Await _vm.ReclamarDeudaAsync()

        Assert.AreEqual("Pago pedido 123456", _vm.AsuntoReclamarDeuda, "Se queda en el cuadro de la ficha")
        Dim cuerpo = JObject.Parse(_manejador.Cuerpos.Single())
        Assert.AreEqual("Pago pedido 123456", cuerpo("Descripcion").ToString())
        Assert.AreEqual(0, CType(cuerpo("Efectos"), JArray).Count)
        Assert.AreEqual("https://pago.nuevavision.es/abc", _vm.EnlaceReclamarDeuda)
    End Function

    <TestMethod()>
    Public Async Function ReclamarDeuda_ConConceptoYaEscrito_NoLoVuelveAPedir() As Task
        _vm.AsuntoReclamarDeuda = "Pago señal curso quiromasaje"

        Await _vm.ReclamarDeudaAsync()

        A.CallTo(Function() _dialogos.GetText(A(Of String).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
        Assert.AreEqual("Pago señal curso quiromasaje", JObject.Parse(_manejador.Cuerpos.Single())("Descripcion").ToString())
    End Function

    <TestMethod()>
    Public Async Function ReclamarDeuda_ConEfectosQueCuadran_NoPideConceptoYMandaElPorDefecto() As Task
        _vm.ListaDeudas = New ObjectModel.ObservableCollection(Of ExtractoClienteDTO) From {
            New ExtractoClienteDTO With {.Id = 7, .ImportePendiente = 25D, .Documento = "NV26/1", .Seleccionada = True}
        }
        _vm.AsuntoReclamarDeuda = ClientesViewModel.ASUNTO_PAGO_POR_DEFECTO

        Await _vm.ReclamarDeudaAsync()

        A.CallTo(Function() _dialogos.GetText(A(Of String).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
        Dim cuerpo = JObject.Parse(_manejador.Cuerpos.Single())
        Assert.AreEqual(1, CType(cuerpo("Efectos"), JArray).Count)
    End Function

    <TestMethod()>
    Public Async Function ReclamarDeuda_EfectosQueNoCuadranConElImporte_VanSinEfectosYPideElConcepto() As Task
        ' Lo que le pasó a Reina: efectos marcados (asunto por defecto) e importe cambiado a mano -> sin efectos (Nesto#438)
        _vm.ListaDeudas = New ObjectModel.ObservableCollection(Of ExtractoClienteDTO) From {
            New ExtractoClienteDTO With {.Id = 7, .ImportePendiente = 40D, .Documento = "NV26/1", .Seleccionada = True}
        }
        _vm.AsuntoReclamarDeuda = ClientesViewModel.ASUNTO_PAGO_POR_DEFECTO
        A.CallTo(Function() _dialogos.GetText(A(Of String).Ignored, A(Of String).Ignored)).Returns("Pago parcial factura NV26/1")

        Await _vm.ReclamarDeudaAsync()

        Dim cuerpo = JObject.Parse(_manejador.Cuerpos.Single())
        Assert.AreEqual("Pago parcial factura NV26/1", cuerpo("Descripcion").ToString())
        Assert.AreEqual(0, CType(cuerpo("Efectos"), JArray).Count)
    End Function

    <TestMethod()>
    Public Async Function ReclamarDeuda_LaApiLoRechazaPorValidacion_EnsenaSuMensajeTalCualYNoLoRegistra() As Task
        _vm.AsuntoReclamarDeuda = "Pago pedido 123456"
        Const MENSAJE As String = "Indique un concepto que identifique el pago (por ejemplo 'Pago pedido 123456' o 'Pago señal curso quiromasaje') o seleccione los efectos que paga el cliente."
        _manejador.Respuesta = Function() New HttpResponseMessage(HttpStatusCode.BadRequest) With {
            .Content = New StringContent(New JObject From {{"Message", MENSAJE}}.ToString())
        }

        Await _vm.ReclamarDeudaAsync()

        A.CallTo(Sub() _dialogos.ShowError(MENSAJE)).MustHaveHappenedOnceExactly()
        A.CallTo(Function() _errores.RegistrarErrorAsync(A(Of Exception).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
        A.CallTo(Sub() _dialogos.ShowDialog(A(Of String).Ignored, A(Of ParametrosDialogo).Ignored, A(Of Action(Of ResultadoDialogo)).Ignored)) _
            .MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function ReclamarDeuda_ErrorDelServidor_SeSigueRegistrando() As Task
        _vm.AsuntoReclamarDeuda = "Pago pedido 123456"
        _manejador.Respuesta = Function() New HttpResponseMessage(HttpStatusCode.InternalServerError) With {
            .Content = New StringContent("{""Message"":""An error has occurred.""}")
        }

        Await _vm.ReclamarDeudaAsync()

        A.CallTo(Function() _errores.RegistrarErrorAsync(A(Of Exception).Ignored, A(Of String).Ignored)).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Sub MensajeValidacionApi_TextoQueNoEsJson_TalCual()
        Assert.AreEqual("Importe no válido", ClientesViewModel.MensajeValidacionApi(" Importe no válido "))
    End Sub
End Class
