Imports System.Collections.Generic
Imports System.Collections.ObjectModel
Imports System.Linq
Imports System.Net
Imports System.Net.Http
Imports System.Threading
Imports System.Threading.Tasks
Imports ControlesUsuario.Dialogs
Imports FakeItEasy
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.[Shared]
Imports Nesto.Modulos.Inventario
Imports Nesto.Modulos.Inventario.InventarioModel

''' <summary>Lo que contesta el usuario al diálogo de confirmar escribiendo (ConfirmacionEscribiendoDialog).</summary>
Public Module DialogosInventarioPrueba
    Public Function ConfirmacionEscribiendo(dialogos As IServicioDialogos) As FakeItEasy.Configuration.IVoidArgumentValidationConfiguration
        Return A.CallTo(Sub() dialogos.ShowDialog(ConfirmacionEscribiendoDialogViewModel.NOMBRE, A(Of ParametrosDialogo).Ignored, A(Of Action(Of ResultadoDialogo)).Ignored))
    End Function

    ''' <param name="escrito">Lo que escribe y acepta; Nothing = pulsa «Cancelar».</param>
    Public Sub ResponderConfirmacionEscribiendo(dialogos As IServicioDialogos, escrito As String)
        ConfirmacionEscribiendo(dialogos).Invokes(
            Sub(nombre As String, parametros As ParametrosDialogo, alCerrar As Action(Of ResultadoDialogo))
                If escrito Is Nothing Then
                    alCerrar(New ResultadoDialogo(ResultadoBoton.Cancel))
                Else
                    alCerrar(New ResultadoDialogo(ResultadoBoton.OK, New ParametrosDialogo From {{"text", escrito}}))
                End If
            End Sub)
    End Sub
End Module

''' <summary>
''' 25/11/26 (Paloma rellenó sin querer 3.032 filas de Alcobendas; no era la primera vez): para rellenar hay que escribir el
''' nombre o el código del almacén, con la cifra de la Previa; lo rellenado se puede deshacer. Y (novedad 1456) «Borrar» en la
''' lista de las filas que la API marca como borrables.
''' </summary>
<TestClass()>
Public Class InventarioRellenarSeguroTests

    Private ReadOnly _dia As Date = Date.Today
    Private _configuracion As IConfiguracion
    Private _dialogos As IServicioDialogos
    Private _servicio As IServicioInventario
    Private _parametrosDialogo As ParametrosDialogo
    Private _previa As PreviaRellenarInventarioDTO

    <TestInitialize()>
    Public Sub Inicializar()
        _configuracion = A.Fake(Of IConfiguracion)()
        _dialogos = A.Fake(Of IServicioDialogos)()
        _servicio = A.Fake(Of IServicioInventario)()
        A.CallTo(Function() _configuracion.leerParametro(A(Of String).Ignored, Parametros.Claves.AlmacenInventario)).Returns(Task.FromResult("ALC"))
        A.CallTo(Function() _servicio.LeerPendientes(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored)) _
            .Returns(Task.FromResult(New List(Of InventarioPendienteDTO)))
        _previa = New PreviaRellenarInventarioDTO With {
            .Almacen = "ALC", .NombreAlmacen = "Alcobendas", .Fecha = _dia, .FilasQueSeCrearian = 3032,
            .Mensaje = "Va a crear 3.032 filas de inventario en Alcobendas con fecha 25/11/2026."}
        A.CallTo(Function() _servicio.PreviaRellenar(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored)).ReturnsLazily(Function() Task.FromResult(_previa))
        A.CallTo(Function() _servicio.Rellenar(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored, A(Of Boolean).Ignored, A(Of String).Ignored)) _
            .ReturnsLazily(Function()
                               ' Tras rellenar, la Previa dice que se puede deshacer.
                               _previa = New PreviaRellenarInventarioDTO With {.Almacen = "ALC", .NombreAlmacen = "Alcobendas", .FilasPendientesYa = 3032, .PuedeDeshacer = True}
                               Return Task.FromResult(New RellenarInventarioRespuestaDTO With {.Almacen = "ALC", .Filas = 3032, .Unidades = 9000})
                           End Function)
        A.CallTo(Function() _servicio.LeerLineas(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored)) _
            .Returns(Task.FromResult(New List(Of InventarioDTO)))
    End Sub

    Private Sub Escribe(texto As String)
        DialogosInventarioPrueba.ConfirmacionEscribiendo(_dialogos).Invokes(
            Sub(nombre As String, parametros As ParametrosDialogo, alCerrar As Action(Of ResultadoDialogo))
                _parametrosDialogo = parametros
                If texto Is Nothing Then
                    alCerrar(New ResultadoDialogo(ResultadoBoton.Cancel))
                Else
                    alCerrar(New ResultadoDialogo(ResultadoBoton.OK, New ParametrosDialogo From {{"text", texto}}))
                End If
            End Sub)
    End Sub

    Private Function CrearViewModel() As InventarioViewModel
        Dim vm As New InventarioViewModel(A.Fake(Of IServicioNavegacion)(), _configuracion, _dialogos, _servicio)
        vm.fechaSeleccionada = _dia
        Return vm
    End Function

    <TestMethod()>
    Public Async Function Rellenar_EnsenaLaCifraDeLaPreviaYPideElNombreOElCodigo() As Task
        Escribe("alcobendas")
        Dim vm = CrearViewModel()

        Await vm.ComprobarRellenoAlAbrir()

        Assert.AreEqual("Va a crear 3.032 filas de inventario en Alcobendas con fecha 25/11/2026.", _parametrosDialogo.GetValue(Of String)("message"))
        Dim aceptables = _parametrosDialogo.GetValue(Of IEnumerable(Of String))(ConfirmacionEscribiendoDialogViewModel.PARAMETRO_ACEPTABLES).ToList()
        CollectionAssert.AreEquivalent({"ALC", "Alcobendas"}, aceptables)
        Assert.AreEqual(InventarioViewModel.TEXTO_BOTON_RELLENAR, _parametrosDialogo.GetValue(Of String)(ConfirmacionEscribiendoDialogViewModel.PARAMETRO_TEXTO_ACEPTAR))
        A.CallTo(Function() _servicio.Rellenar("1", "ALC", _dia, False, "ALC")).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function Rellenar_Cancela_NoRellena() As Task
        Escribe(Nothing)
        Dim vm = CrearViewModel()

        Await vm.ComprobarRellenoAlAbrir()

        A.CallTo(Function() _servicio.Rellenar(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored, A(Of Boolean).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
        Assert.IsFalse(vm.PuedeDeshacerRellenado)
    End Function

    <TestMethod()>
    Public Async Function Rellenar_EscribeOtraCosa_NoRellena() As Task
        Escribe("Reina")
        Dim vm = CrearViewModel()

        Await vm.ComprobarRellenoAlAbrir()

        A.CallTo(Function() _servicio.Rellenar(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored, A(Of Boolean).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function Rellenar_PreviaFalla_PideIgualElNombreSinLaCifra() As Task
        A.CallTo(Function() _servicio.PreviaRellenar(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored)) _
            .ReturnsLazily(Function() Task.FromResult(Of PreviaRellenarInventarioDTO)(Nothing))
        Escribe("ALC")
        Dim vm = CrearViewModel()

        Await vm.ComprobarRellenoAlAbrir()

        Dim mensaje = _parametrosDialogo.GetValue(Of String)("message")
        StringAssert.Contains(mensaje, "Alcobendas")
        Assert.IsFalse(mensaje.Contains("filas"), "Sin Previa no se sabe cuántas filas.")
        CollectionAssert.AreEquivalent({"ALC", "Alcobendas"}, _parametrosDialogo.GetValue(Of IEnumerable(Of String))(ConfirmacionEscribiendoDialogViewModel.PARAMETRO_ACEPTABLES).ToList())
        A.CallTo(Function() _servicio.Rellenar("1", "ALC", _dia, False, "ALC")).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function TrasRellenar_SeOfreceDeshacer() As Task
        Escribe("ALC")
        Dim vm = CrearViewModel()

        Await vm.ComprobarRellenoAlAbrir()

        Assert.IsTrue(vm.PuedeDeshacerRellenado)
    End Function

    <TestMethod()>
    Public Async Function AlAbrir_YaRellenadoYDeshacible_SeOfreceDeshacer() As Task
        A.CallTo(Function() _servicio.LeerPendientes(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored)) _
            .Returns(Task.FromResult(New List(Of InventarioPendienteDTO) From {New InventarioPendienteDTO With {.Filas = 3032}}))
        _previa = New PreviaRellenarInventarioDTO With {.Almacen = "ALC", .FilasPendientesYa = 3032, .PuedeDeshacer = True}
        Dim vm = CrearViewModel()

        Await vm.ComprobarRellenoAlAbrir()

        Assert.IsTrue(vm.PuedeDeshacerRellenado)
        DialogosInventarioPrueba.ConfirmacionEscribiendo(_dialogos).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function AlAbrir_NoDeshacible_SinBoton() As Task
        A.CallTo(Function() _servicio.LeerPendientes(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored)) _
            .Returns(Task.FromResult(New List(Of InventarioPendienteDTO) From {New InventarioPendienteDTO With {.Filas = 3032}}))
        _previa = New PreviaRellenarInventarioDTO With {.Almacen = "ALC", .FilasPendientesYa = 3032, .PuedeDeshacer = False, .MotivoNoPuedeDeshacer = "Hay filas contadas."}
        Dim vm = CrearViewModel()

        Await vm.ComprobarRellenoAlAbrir()

        Assert.IsFalse(vm.PuedeDeshacerRellenado)
    End Function

    <TestMethod()>
    Public Async Function Deshacer_ConfirmaBorraRecargaYDiceCuantas() As Task
        A.CallTo(Function() _servicio.LeerPendientes(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored)) _
            .Returns(Task.FromResult(New List(Of InventarioPendienteDTO) From {New InventarioPendienteDTO With {.Filas = 3032}}))
        _previa = New PreviaRellenarInventarioDTO With {.Almacen = "ALC", .NombreAlmacen = "Alcobendas", .FilasPendientesYa = 3032, .PuedeDeshacer = True}
        A.CallTo(Function() _dialogos.ShowConfirmationAnswer(InventarioViewModel.TITULO_DESHACER_RELLENADO, A(Of String).Ignored, True)).Returns(True)
        A.CallTo(Function() _servicio.DeshacerRellenado("1", "ALC", _dia)) _
            .ReturnsLazily(Function()
                               _previa = New PreviaRellenarInventarioDTO With {.Almacen = "ALC", .PuedeDeshacer = False}
                               Return Task.FromResult(New DeshacerRellenarInventarioRespuestaDTO With {.Almacen = "ALC", .FilasBorradas = 3032})
                           End Function)
        Dim vm = CrearViewModel()
        Await vm.ComprobarRellenoAlAbrir()

        Await vm.DeshacerRellenado()

        A.CallTo(Function() _dialogos.ShowConfirmationAnswer(InventarioViewModel.TITULO_DESHACER_RELLENADO,
            A(Of String).That.Contains("las 3.032 filas"), True)).MustHaveHappenedOnceExactly()
        A.CallTo(Function() _servicio.DeshacerRellenado("1", "ALC", _dia)).MustHaveHappenedOnceExactly()
        A.CallTo(Function() _servicio.LeerLineas("1", "ALC", _dia)).MustHaveHappened()
        A.CallTo(Sub() _dialogos.ShowNotification(InventarioViewModel.TITULO_DESHACER_RELLENADO, "Se han borrado 3.032 filas.")).MustHaveHappenedOnceExactly()
        Assert.IsFalse(vm.PuedeDeshacerRellenado)
    End Function

    <TestMethod()>
    Public Async Function Deshacer_DiceNo_NoBorra() As Task
        A.CallTo(Function() _servicio.LeerPendientes(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored)) _
            .Returns(Task.FromResult(New List(Of InventarioPendienteDTO) From {New InventarioPendienteDTO With {.Filas = 5}}))
        _previa = New PreviaRellenarInventarioDTO With {.Almacen = "ALC", .FilasPendientesYa = 5, .PuedeDeshacer = True}
        A.CallTo(Function() _dialogos.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored, A(Of Boolean).Ignored)).Returns(False)
        Dim vm = CrearViewModel()
        Await vm.ComprobarRellenoAlAbrir()

        Await vm.DeshacerRellenado()

        A.CallTo(Function() _servicio.DeshacerRellenado(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function Deshacer_LaApiDiceQueNo_EnsenaSuMensaje() As Task
        A.CallTo(Function() _servicio.LeerPendientes(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored)) _
            .Returns(Task.FromResult(New List(Of InventarioPendienteDTO) From {New InventarioPendienteDTO With {.Filas = 5}}))
        _previa = New PreviaRellenarInventarioDTO With {.Almacen = "ALC", .FilasPendientesYa = 5, .PuedeDeshacer = True}
        A.CallTo(Function() _dialogos.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored, A(Of Boolean).Ignored)).Returns(True)
        A.CallTo(Function() _servicio.DeshacerRellenado(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored)) _
            .ThrowsAsync(New Exception("Hay 12 filas ya contadas: solo Dirección o Informática pueden deshacerlo."))
        Dim vm = CrearViewModel()
        Await vm.ComprobarRellenoAlAbrir()

        Await vm.DeshacerRellenado()

        A.CallTo(Sub() _dialogos.ShowError("Hay 12 filas ya contadas: solo Dirección o Informática pueden deshacerlo.")).MustHaveHappenedOnceExactly()
        A.CallTo(Sub() _dialogos.ShowNotification(A(Of String).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Sub MensajeRellenadoDeshecho_SingularYPlural()
        Assert.AreEqual("Se ha borrado 1 fila.", InventarioViewModel.MensajeRellenadoDeshecho(New DeshacerRellenarInventarioRespuestaDTO With {.FilasBorradas = 1}))
        Assert.AreEqual("Se han borrado 3.032 filas. 2 de ellas ya estaban contadas.",
                        InventarioViewModel.MensajeRellenadoDeshecho(New DeshacerRellenarInventarioRespuestaDTO With {.FilasBorradas = 3032, .FilasContadas = 2}))
    End Sub

    ' --- Borrar una fila desde la lista (novedad 1456) ---

    <TestMethod()>
    Public Sub BorrarFila_SoloSiLaApiLaMarca()
        Dim vm = CrearViewModel()

        Assert.IsTrue(vm.cmdBorrarFila.CanExecute(New InventarioDTO With {.NumOrden = 7, .SePuedeBorrar = True}))
        Assert.IsFalse(vm.cmdBorrarFila.CanExecute(New InventarioDTO With {.NumOrden = 7, .SePuedeBorrar = False}))
        Assert.IsFalse(vm.cmdBorrarFila.CanExecute(New InventarioDTO With {.NumOrden = 0, .SePuedeBorrar = True}))
    End Sub

    <TestMethod()>
    Public Async Function BorrarFila_ConfirmaComoLasComprobacionesBorraYRecarga() As Task
        Dim fila As New InventarioDTO With {.NumOrden = 948787, .Producto = "17404", .Descripcion = "REPUESTO", .Fecha = _dia.AddDays(-1),
            .StockCalculado = 2, .StockReal = 1, .SePuedeBorrar = True}
        A.CallTo(Function() _dialogos.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored, A(Of Boolean).Ignored)).Returns(True)
        Dim vm = CrearViewModel()

        Await vm.BorrarFila(fila)

        Dim esperado = ComprobacionesInventarioDialogViewModel.TextoConfirmarBorrado(InventarioViewModel.ComoFilaComprobacion(fila))
        A.CallTo(Function() _dialogos.ShowConfirmationAnswer(ComprobacionesInventarioDialogViewModel.TITULO_BORRAR_FILA, esperado, True)).MustHaveHappenedOnceExactly()
        StringAssert.Contains(esperado, "del recuento del " + _dia.AddDays(-1).ToString("dd/MM/yyyy"))
        A.CallTo(Function() _servicio.BorrarFila(948787)).MustHaveHappenedOnceExactly()
        A.CallTo(Function() _servicio.LeerLineas("1", "ALC", _dia)).MustHaveHappenedOnceExactly()
        A.CallTo(Sub() _dialogos.ShowNotification(A(Of String).Ignored, InventarioViewModel.MENSAJE_FILA_BORRADA)).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function BorrarFila_DiceNo_NoBorra() As Task
        A.CallTo(Function() _dialogos.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored, A(Of Boolean).Ignored)).Returns(False)
        Dim vm = CrearViewModel()

        Await vm.BorrarFila(New InventarioDTO With {.NumOrden = 7, .SePuedeBorrar = True})

        A.CallTo(Function() _servicio.BorrarFila(A(Of Integer).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function BorrarFila_409_EnsenaElMensajeYNoRecarga() As Task
        A.CallTo(Function() _dialogos.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored, A(Of Boolean).Ignored)).Returns(True)
        A.CallTo(Function() _servicio.BorrarFila(7)).ThrowsAsync(New ConflictoInventarioException("Esa fila ya está contabilizada."))
        Dim vm = CrearViewModel()

        Await vm.BorrarFila(New InventarioDTO With {.NumOrden = 7, .SePuedeBorrar = True})

        A.CallTo(Sub() _dialogos.ShowError("Esa fila ya está contabilizada.")).MustHaveHappenedOnceExactly()
        A.CallTo(Function() _servicio.LeerLineas(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored)).MustNotHaveHappened()
        A.CallTo(Sub() _dialogos.ShowNotification(A(Of String).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function BorrarFila_YaNoExiste_AvisaYRecarga() As Task
        A.CallTo(Function() _dialogos.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored, A(Of Boolean).Ignored)).Returns(True)
        A.CallTo(Function() _servicio.BorrarFila(7)).ThrowsAsync(New FilaInventarioNoEncontradaException(ServicioInventario.MENSAJE_FILA_NO_ENCONTRADA))
        Dim vm = CrearViewModel()

        Await vm.BorrarFila(New InventarioDTO With {.NumOrden = 7, .SePuedeBorrar = True})

        A.CallTo(Sub() _dialogos.ShowError(ServicioInventario.MENSAJE_FILA_NO_ENCONTRADA)).MustHaveHappenedOnceExactly()
        A.CallTo(Function() _servicio.LeerLineas("1", "ALC", _dia)).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Sub InventarioDTO_SePuedeBorrar_SeLeePeroNoSeManda()
        Dim fila = Newtonsoft.Json.JsonConvert.DeserializeObject(Of InventarioDTO)("{""$id"":""1"",""NºOrden"":7,""sepuedeborrar"":true}")
        Assert.IsTrue(fila.SePuedeBorrar)
        Assert.AreEqual(7, fila.NumOrden)
        StringAssert.DoesNotMatch(Newtonsoft.Json.JsonConvert.SerializeObject(fila), New Text.RegularExpressions.Regex("SePuedeBorrar"))
    End Sub

    ' --- Servicio (HTTP falso) ---

    Private Class ManejadorFalso
        Inherits HttpMessageHandler

        Public ReadOnly Peticiones As New List(Of String)
        Public ReadOnly Cuerpos As New List(Of String)
        Public Property Estado As HttpStatusCode = HttpStatusCode.OK
        Public Property Cuerpo As String = "{}"

        Protected Overrides Async Function SendAsync(request As HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
            Peticiones.Add(request.Method.Method + " " + request.RequestUri.PathAndQuery)
            Cuerpos.Add(If(request.Content Is Nothing, Nothing, Await request.Content.ReadAsStringAsync()))
            Return New HttpResponseMessage(Estado) With {.Content = New StringContent(Cuerpo)}
        End Function
    End Class

    Private Function CrearServicio(manejador As ManejadorFalso) As ServicioInventario
        Dim factoria = A.Fake(Of IClienteApiFactory)()
        A.CallTo(Function() factoria.Crear()).ReturnsLazily(Function() New HttpClient(manejador, False) With {.BaseAddress = New Uri("http://localhost/api/")})
        Return New ServicioInventario(factoria)
    End Function

    <TestMethod()>
    Public Async Function Servicio_Rellenar_MandaConfirmarEnElCuerpo() As Task
        Dim manejador As New ManejadorFalso With {.Cuerpo = "{""Almacen"":""ALC"",""Filas"":3}"}

        Await CrearServicio(manejador).Rellenar("1", "ALC", New Date(2026, 11, 25), False, "ALC")

        StringAssert.Contains(manejador.Cuerpos.Single(), """Confirmar"":""ALC""")
    End Function

    <TestMethod()>
    Public Async Function Servicio_Previa_UrlYLectura() As Task
        Dim manejador As New ManejadorFalso With {
            .Cuerpo = "{""$id"":""1"",""almacen"":""ALC"",""nombreAlmacen"":""Alcobendas"",""filasQueSeCrearian"":3032,""mensaje"":""Va a crear 3.032 filas"",""puedeDeshacer"":false}"}

        Dim previa = Await CrearServicio(manejador).PreviaRellenar("1", "ALC", New Date(2026, 11, 25))

        Assert.AreEqual("GET /api/Inventarios/Rellenar/Previa?almacen=ALC&fecha=2026-11-25&empresa=1", manejador.Peticiones.Single())
        Assert.AreEqual(3032, previa.FilasQueSeCrearian)
        Assert.AreEqual("Alcobendas", previa.NombreAlmacen)
    End Function

    <TestMethod()>
    Public Async Function Servicio_Previa_ServidorAntiguo_Nothing() As Task
        Dim manejador As New ManejadorFalso With {.Estado = HttpStatusCode.NotFound,
            .Cuerpo = "{""Message"":""No HTTP resource was found that matches the request URI"",""MessageDetail"":""x""}"}

        Assert.IsNull(Await CrearServicio(manejador).PreviaRellenar("1", "ALC", New Date(2026, 11, 25)))
    End Function

    <TestMethod()>
    Public Async Function Servicio_Deshacer_UrlY403ConSuMensaje() As Task
        Dim manejador As New ManejadorFalso With {.Cuerpo = "{""Almacen"":""ALC"",""FilasBorradas"":3032}"}
        Dim respuesta = Await CrearServicio(manejador).DeshacerRellenado("1", "ALC", New Date(2026, 11, 25))
        Assert.AreEqual("DELETE /api/Inventarios/Rellenar?almacen=ALC&fecha=2026-11-25&empresa=1", manejador.Peticiones.Single())
        Assert.AreEqual(3032, respuesta.FilasBorradas)

        Dim prohibido As New ManejadorFalso With {.Estado = HttpStatusCode.Forbidden, .Cuerpo = "{""Message"":""Solo quien lo rellenó puede deshacerlo.""}"}
        Dim ex = Await Assert.ThrowsExceptionAsync(Of Exception)(Function() CrearServicio(prohibido).DeshacerRellenado("1", "ALC", New Date(2026, 11, 25)))
        Assert.AreEqual("Solo quien lo rellenó puede deshacerlo.", ex.Message)
    End Function
End Class
