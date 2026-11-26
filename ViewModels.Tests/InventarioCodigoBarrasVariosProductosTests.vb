Imports System.Collections.Generic
Imports System.Net
Imports System.Net.Http
Imports System.Threading
Imports System.Threading.Tasks
Imports ControlesUsuario.Models
Imports FakeItEasy
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.[Shared]
Imports Nesto.Modulos.Inventario
Imports Nesto.Modulos.Inventario.InventarioModel

''' <summary>
''' Nesto#647: un código de barras que llevan varios productos ya no da «teclee la referencia» en el inventario: se pregunta
''' cuál es (el selector común) y se cuenta en ese.
''' </summary>
<TestClass()>
Public Class InventarioCodigoBarrasVariosProductosTests

    Private Const EAN As String = "8437017506362"
    Private ReadOnly _dia As New Date(2026, 11, 26)
    Private _configuracion As IConfiguracion
    Private _dialogos As IServicioDialogos
    Private _servicio As IServicioInventario
    Private _creadas As List(Of InventarioDTO)

    Private Class ManejadorFalso
        Inherits HttpMessageHandler

        Public Property Respuesta As HttpResponseMessage

        Protected Overrides Function SendAsync(request As HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
            Return Task.FromResult(Respuesta)
        End Function
    End Class

    <TestInitialize()>
    Public Sub Inicializar()
        _configuracion = A.Fake(Of IConfiguracion)()
        _dialogos = A.Fake(Of IServicioDialogos)()
        _servicio = A.Fake(Of IServicioInventario)()
        _creadas = New List(Of InventarioDTO)
        A.CallTo(Function() _configuracion.leerParametro(A(Of String).Ignored, Parametros.Claves.AlmacenInventario)) _
            .Returns(Task.FromResult("REI"))
        A.CallTo(Function() _servicio.LeerPendientes(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored)) _
            .Returns(Task.FromResult(New List(Of InventarioPendienteDTO) From {New InventarioPendienteDTO With {.Filas = 64}}))
        A.CallTo(Function() _servicio.BuscarLinea(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored, EAN)) _
            .ThrowsAsync(New ConflictoInventarioException("El código lo llevan varios productos.", New ConflictoInventarioDTO With {
                .Productos = New List(Of ProductoCodigoBarrasDuplicado) From {
                    New ProductoCodigoBarrasDuplicado With {.Producto = "41000", .Nombre = "GUANTES M"},
                    New ProductoCodigoBarrasDuplicado With {.Producto = "41001", .Nombre = "GUANTES P"}
                }
            }))
        A.CallTo(Function() _servicio.BuscarLinea(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored, "41001")) _
            .Returns(Task.FromResult(Of InventarioDTO)(Nothing))
        A.CallTo(Function() _servicio.CrearLinea(A(Of InventarioDTO).Ignored)) _
            .ReturnsLazily(Function(l As InventarioDTO)
                               _creadas.Add(l)
                               Return Task.FromResult(New InventarioDTO With {.Producto = l.Producto, .Descripcion = "GUANTES P"})
                           End Function)
    End Sub

    Private Function CrearViewModel() As InventarioViewModel
        Dim vm As New InventarioViewModel(A.Fake(Of IServicioNavegacion)(), _configuracion, _dialogos, _servicio)
        vm.fechaSeleccionada = _dia
        Return vm
    End Function

    Private Sub Elige(producto As String)
        A.CallTo(Function() _dialogos.ShowDialogAsync("SelectorProductoDuplicadoDialog", A(Of ParametrosDialogo).Ignored)) _
            .Returns(Task.FromResult(New ResultadoDialogo(ResultadoBoton.OK, New ParametrosDialogo From {{"producto", producto}})))
    End Sub

    <TestMethod()>
    Public Async Function CodigoDeVariosProductos_PreguntaYCuentaEnElElegido() As Task
        Elige("41001")
        Dim vm = CrearViewModel()

        Dim movimiento = Await vm.InsertarProducto(EAN)

        Assert.IsNotNull(movimiento)
        Assert.AreEqual(1, _creadas.Count)
        Assert.AreEqual("41001", _creadas(0).Producto)
        A.CallTo(Sub() _dialogos.ShowError(A(Of String).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function CodigoDeVariosProductos_Cancela_NoCuentaNadaNiDaError() As Task
        A.CallTo(Function() _dialogos.ShowDialogAsync("SelectorProductoDuplicadoDialog", A(Of ParametrosDialogo).Ignored)) _
            .Returns(Task.FromResult(New ResultadoDialogo(ResultadoBoton.Cancel)))
        Dim vm = CrearViewModel()

        Dim movimiento = Await vm.InsertarProducto(EAN)

        Assert.IsNull(movimiento)
        A.CallTo(Function() _servicio.CrearLinea(A(Of InventarioDTO).Ignored)).MustNotHaveHappened()
        A.CallTo(Sub() _dialogos.ShowError(A(Of String).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function DejarACero_CodigoDeVariosProductos_PreguntaYLoHaceConElElegido() As Task
        Elige("41001")
        A.CallTo(Function() _configuracion.UsuarioEnGrupo(A(Of String).Ignored)).Returns(True)
        A.CallTo(Function() _dialogos.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored, A(Of Boolean).Ignored)).Returns(True)
        A.CallTo(Function() _servicio.DejarACero(A(Of String).Ignored, A(Of String).Ignored, EAN, A(Of Boolean).Ignored, A(Of Boolean).Ignored, A(Of Boolean).Ignored)) _
            .ThrowsAsync(New ConflictoInventarioException("Varios", New ConflictoInventarioDTO With {
                .Productos = New List(Of ProductoCodigoBarrasDuplicado) From {
                    New ProductoCodigoBarrasDuplicado With {.Producto = "41000"},
                    New ProductoCodigoBarrasDuplicado With {.Producto = "41001"}
                }
            }))
        A.CallTo(Function() _servicio.DejarACero(A(Of String).Ignored, A(Of String).Ignored, "41001", A(Of Boolean).Ignored, A(Of Boolean).Ignored, A(Of Boolean).Ignored)) _
            .Returns(Task.FromResult(New DejarACeroInventarioRespuestaDTO With {
                .Almacen = "REI", .Producto = "41001", .FilasCreadas = 1,
                .Lineas = New List(Of LineaDejarACeroDTO) From {New LineaDejarACeroDTO With {.StockCalculado = 3, .Accion = LineaDejarACeroDTO.CREAR}}
            }))
        Dim vm = CrearViewModel()
        vm.numeroProducto = EAN

        Await vm.DejarACeroEnUbicaciones()

        A.CallTo(Function() _servicio.DejarACero("1", "REI", "41001", False, False, False)).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function Servicio_409ConProductos_TraeLosCandidatos() As Task
        Dim manejador As New ManejadorFalso With {
            .Respuesta = New HttpResponseMessage(HttpStatusCode.Conflict) With {
                .Content = New StringContent("{""Message"":""El código 8437017506362 lo llevan varios productos."",""Productos"":[{""Producto"":""41000"",""Nombre"":""GUANTES M"",""Cantidad"":1},{""Producto"":""41001"",""Nombre"":""GUANTES P"",""Cantidad"":1}]}")
            }
        }
        Dim factoria = A.Fake(Of IClienteApiFactory)()
        A.CallTo(Function() factoria.Crear()).ReturnsLazily(Function() New HttpClient(manejador, False) With {.BaseAddress = New Uri("http://localhost/api/")})
        Dim servicio As New ServicioInventario(factoria)

        Dim ex = Await Assert.ThrowsExceptionAsync(Of ConflictoInventarioException)(Function() servicio.BuscarLinea("1", "REI", _dia, EAN))

        Assert.IsTrue(ex.EsCodigoDeVariosProductos)
        Assert.AreEqual(2, ex.Candidatos.Count)
        Assert.AreEqual("41001", ex.Candidatos(1).Producto)
    End Function
End Class
