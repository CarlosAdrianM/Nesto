Imports System.Collections.ObjectModel
Imports System.Net
Imports System.Net.Http
Imports System.Threading
Imports System.Threading.Tasks
Imports ControlesUsuario.Dialogs
Imports FakeItEasy
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Models
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.Modulos.Rapports
Imports Nesto.ViewModels
Imports Newtonsoft.Json.Linq

' Nesto#644 (auditoría de la ficha de cliente, 17/11/26), tanda 2: hallazgos 5 a 15. Un test de regresión por hallazgo
' (los que son de la API, 7 y 15, no tienen test aquí).
<TestClass()>
Public Class ClienteFichaAuditoria644bTests

    ''' <summary>La API falsa: apunta cada petición (método, url y cuerpo) y contesta lo que diga <see cref="Respuesta"/>.</summary>
    Private Class ApiFalsa
        Inherits HttpMessageHandler

        Public ReadOnly Peticiones As New List(Of (Metodo As String, Url As String, Cuerpo As String))
        Public Property Respuesta As Func(Of HttpRequestMessage, Task(Of HttpResponseMessage)) =
            Function(r) Task.FromResult(New HttpResponseMessage(HttpStatusCode.NoContent))

        Protected Overrides Async Function SendAsync(request As HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
            Dim cuerpo As String = If(request.Content Is Nothing, Nothing, Await request.Content.ReadAsStringAsync())
            SyncLock Peticiones
                Peticiones.Add((request.Method.Method, request.RequestUri.PathAndQuery, cuerpo))
            End SyncLock
            Return Await Respuesta.Invoke(request)
        End Function

        Public Function Puts() As List(Of JObject)
            Return Peticiones.Where(Function(p) p.Metodo = "PUT").Select(Function(p) JObject.Parse(p.Cuerpo)).ToList()
        End Function
    End Class

    Private _servicio As IClienteComercialService
    Private _dialogos As IServicioDialogos
    Private _api As ApiFalsa
    Private _factoria As IClienteApiFactory
    Private _autenticacion As IServicioAutenticacion
    Private _rapports As IRapportService

    <TestInitialize()>
    Public Sub Initialize()
        _servicio = A.Fake(Of IClienteComercialService)()
        _dialogos = A.Fake(Of IServicioDialogos)()
        _rapports = A.Fake(Of IRapportService)()
        _api = New ApiFalsa()
        _factoria = A.Fake(Of IClienteApiFactory)()
        A.CallTo(Function() _factoria.Crear()).ReturnsLazily(Function() New HttpClient(_api, False) With {
            .BaseAddress = New Uri("http://localhost/api/")
        })
        _autenticacion = A.Fake(Of IServicioAutenticacion)()
        A.CallTo(Function() _autenticacion.ConfigurarAutorizacion(A(Of HttpClient).Ignored)).Returns(Task.FromResult(True))
        ' Todas las confirmaciones («¿Desea guardar…?») se aceptan
        A.CallTo(Sub() _dialogos.ShowDialog("ConfirmationDialog", A(Of ParametrosDialogo).Ignored, A(Of Action(Of ResultadoDialogo)).Ignored)) _
            .Invokes(Sub(nombre As String, parametros As ParametrosDialogo, callback As Action(Of ResultadoDialogo)) callback(New ResultadoDialogo(ResultadoBoton.OK)))
    End Sub

    Private Function Comercial() As ClienteComercialViewModel
        Return New ClienteComercialViewModel(A.Fake(Of IConfiguracion)(), _dialogos, _servicio, _rapports, _autenticacion) With {
            .ClienteApiFactory = _factoria
        }
    End Function

    Private Shared Function Ficha(Optional conPeluqueria As Boolean = True) As ClienteJson
        Dim cliente As New ClienteJson With {
            .empresa = "1", .cliente = "15191", .contacto = "0", .nombre = "EL EDÉN", .estado = 0, .vendedor = "JE", .pais = "ES",
            .VendedoresGrupoProducto = New ObservableCollection(Of VendedorGrupoProductoDTO)()
        }
        If conPeluqueria Then
            cliente.VendedoresGrupoProducto.Add(New VendedorGrupoProductoDTO With {.grupoProducto = "PEL", .vendedor = "JE", .estado = 0})
        End If
        Return cliente
    End Function

    ''' <summary>La ficha cargada como en la pantalla: leída de la API y con los vendedores por grupo puestos.</summary>
    Private Async Function ConFicha(ficha As ClienteJson) As Task(Of ClienteComercialViewModel)
        A.CallTo(Function() _servicio.LeerCliente("1", "15191", "0")).Returns(Task.FromResult(ficha))
        Dim vm = Comercial()
        Await vm.ActualizarClienteAsync("1", "15191", "0")
        vm.cargarVendedoresPorGrupo()
        Return vm
    End Function

    ' Hallazgo 11: si la API no contesta (red caída), el fallo solo iba a mensajeError, que ClienteComercial.xaml no enseña.
    <TestMethod()>
    Public Async Function H11_SiNoSePuedeLlegarALaApi_AlGuardarSeAvisaEnUnaVentana() As Task
        Dim vm = Await ConFicha(Ficha())
        vm.clienteActivo.estado = 5
        _api.Respuesta = Function(r) Task.FromException(Of HttpResponseMessage)(
            New HttpRequestException("Error al enviar la solicitud.", New WebException("No se puede conectar con el servidor remoto")))

        Await vm.GuardarFichaComercialAsync()

        A.CallTo(Sub() _dialogos.ShowError("No se ha guardado el cliente 15191: No se puede conectar con el servidor remoto")).MustHaveHappenedOnceExactly()
        A.CallTo(Sub() _dialogos.ShowDialog("NotificationDialog", A(Of ParametrosDialogo).Ignored, A(Of Action(Of ResultadoDialogo)).Ignored)).MustNotHaveHappened()
    End Function

    ' Hallazgo 11 (también): lo que contesta la API al rechazarlo se enseña tal cual, sin el JSON.
    <TestMethod()>
    Public Async Function H11_SiLaApiLoRechaza_SeEnsenaSuMotivo() As Task
        Dim vm = Await ConFicha(Ficha())
        vm.clienteActivo.estado = 5
        _api.Respuesta = Function(r) Task.FromResult(New HttpResponseMessage(HttpStatusCode.Forbidden) With {
            .Content = New StringContent("""El cliente lo lleva PA, que no es de tu equipo: no puedes cambiarlo""")
        })

        Await vm.GuardarFichaComercialAsync()

        A.CallTo(Sub() _dialogos.ShowError("No se ha guardado el cliente 15191: El cliente lo lleva PA, que no es de tu equipo: no puedes cambiarlo")).MustHaveHappenedOnceExactly()
    End Function

    ' Hallazgo 13: guardar hacía .Result en el hilo de la pantalla; ahora espera sin bloquear.
    <TestMethod()>
    Public Async Function H13_GuardarNoBloqueaMientrasLaApiContesta() As Task
        Dim vm = Await ConFicha(Ficha())
        vm.clienteActivo.estado = 5
        Dim contestacion As New TaskCompletionSource(Of HttpResponseMessage)()
        _api.Respuesta = Function(r) contestacion.Task

        Dim guardado As Task = vm.GuardarFichaComercialAsync()

        Assert.IsFalse(guardado.IsCompleted, "Mientras la API no contesta, la llamada ya ha devuelto el control")
        contestacion.SetResult(New HttpResponseMessage(HttpStatusCode.NoContent))
        Await guardado
        A.CallTo(Sub() _dialogos.ShowDialog("NotificationDialog", A(Of ParametrosDialogo).Ignored, A(Of Action(Of ResultadoDialogo)).Ignored)).MustHaveHappenedOnceExactly()
    End Function
End Class
