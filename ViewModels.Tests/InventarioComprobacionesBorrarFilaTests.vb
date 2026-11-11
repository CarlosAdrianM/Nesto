Imports System.Collections.Generic
Imports System.Linq
Imports System.Net
Imports System.Net.Http
Imports System.Threading
Imports System.Threading.Tasks
Imports FakeItEasy
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.[Shared]
Imports Nesto.Modulos.Inventario
Imports Nesto.Modulos.Inventario.InventarioModel

''' <summary>
''' Incidencia 1186 (11/11/26): un mozo tenía una fila bloqueante que sobraba en «Comprobaciones del inventario» y la ventana no
''' le dejaba hacer nada con ella (hubo que borrarla por SQL). Ahora «Borrar esta fila» (DELETE api/Inventarios/Filas/{Nº orden})
''' y se vuelven a pedir las comprobaciones.
''' </summary>
<TestClass()>
Public Class InventarioComprobacionesBorrarFilaTests

    Private ReadOnly _dia As New Date(2026, 11, 21)
    Private _dialogos As IServicioDialogos
    Private _servicio As IServicioInventario

    <TestInitialize()>
    Public Sub Inicializar()
        _dialogos = A.Fake(Of IServicioDialogos)()
        _servicio = A.Fake(Of IServicioInventario)()
        Confirma(True)
    End Sub

    Private Sub Confirma(si As Boolean)
        A.CallTo(Function() _dialogos.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored, A(Of Boolean).Ignored)).Returns(si)
    End Sub

    Private Shared Function Fila(numOrden As Integer?, Optional sePuedeBorrar As Boolean = True) As FilaComprobacionInventarioDTO
        Return New FilaComprobacionInventarioDTO With {
            .NumOrden = numOrden, .SePuedeBorrar = sePuedeBorrar, .Producto = "17404     ", .Descripcion = "Champú", .StockCalculado = 3, .StockReal = 5,
            .Usuario = "NUEVAVISION\Andre"}
    End Function

    Private Function ConBloqueante(codigo As String, ParamArray filas As FilaComprobacionInventarioDTO()) As ComprobacionesInventarioDTO
        Dim c As New ComprobacionesInventarioDTO With {.Almacen = "ALG", .Fecha = _dia}
        c.Bloqueantes.Add(New ComprobacionInventarioDTO With {.Codigo = codigo, .Titulo = "Productos que se ubican contados sin ubicación",
            .Filas = filas.ToList()})
        c.Recuento.Add(New RecuentoUsuarioInventarioDTO With {.Usuario = "NUEVAVISION\Andre", .Filas = 10})
        Return c
    End Function

    Private Function SinNada() As ComprobacionesInventarioDTO
        Dim c As New ComprobacionesInventarioDTO With {.Almacen = "ALG", .Fecha = _dia}
        c.Recuento.Add(New RecuentoUsuarioInventarioDTO With {.Usuario = "NUEVAVISION\Andre", .Filas = 9})
        Return c
    End Function

    Private Function CrearDialogo(comprobaciones As ComprobacionesInventarioDTO) As ComprobacionesInventarioDialogViewModel
        Dim dialogo As New ComprobacionesInventarioDialogViewModel(_servicio, _dialogos)
        dialogo.OnDialogOpened(New ParametrosDialogo From {
            {ComprobacionesInventarioDialogViewModel.PARAMETRO_COMPROBACIONES, comprobaciones},
            {ComprobacionesInventarioDialogViewModel.PARAMETRO_EMPRESA, "1"}
        })
        Return dialogo
    End Function

    <TestMethod()>
    Public Async Function Borrar_LlamaConElNumeroDeOrdenYRefresca_YSinBloqueantesDejaContinuar() As Task
        A.CallTo(Function() _servicio.Comprobaciones("1", "ALG", _dia)).Returns(Task.FromResult(SinNada()))
        Dim dialogo = CrearDialogo(ConBloqueante("SinUbicacion", Fila(948787)))
        Dim resultado As ResultadoDialogo = Nothing
        AddHandler dialogo.RequestClose, Sub(r) resultado = r
        Assert.IsFalse(dialogo.ContinuarCommand.CanExecute(Nothing))
        Dim linea = dialogo.Bloqueantes.Single().Filas.Single()
        Assert.IsTrue(linea.SePuedeBorrar)

        Await dialogo.BorrarFila(linea)

        A.CallTo(Function() _servicio.BorrarFila(948787)).MustHaveHappenedOnceExactly()
        A.CallTo(Function() _servicio.Comprobaciones("1", "ALG", _dia)).MustHaveHappenedOnceExactly()
        Assert.IsFalse(dialogo.HayBloqueantes)
        Assert.IsTrue(dialogo.MostrarContinuar)
        Assert.IsTrue(dialogo.ContinuarCommand.CanExecute(Nothing))
        dialogo.ContinuarCommand.Execute(Nothing)
        Assert.AreEqual(ResultadoBoton.OK, resultado.Result)
        Assert.AreEqual(9, resultado.Parameters.GetValue(Of ComprobacionesInventarioDTO)(ComprobacionesInventarioDialogViewModel.PARAMETRO_COMPROBACIONES).Recuento.Single().Filas)
    End Function

    <TestMethod()>
    Public Async Function Borrar_ConfirmaQueBorrarNoEsCeroConLosNumerosDeLaFila_PorDefectoNo() As Task
        Dim dialogo = CrearDialogo(ConBloqueante("SinUbicacion", Fila(12)))

        Dim linea = dialogo.Bloqueantes.Single().Filas.Single()
        Await dialogo.BorrarFila(linea)

        A.CallTo(Function() _dialogos.ShowConfirmationAnswer("Borrar esta fila",
            ComprobacionesInventarioDialogViewModel.TextoConfirmarBorrado(linea), True)).MustHaveHappenedOnceExactly()
        Dim texto = ComprobacionesInventarioDialogViewModel.TextoConfirmarBorrado(Fila(12))
        Assert.AreEqual(String.Join(Environment.NewLine, {
            "Va a BORRAR esta fila del recuento de hoy:",
            "17404 Champú · sin ubicación · calculado 3, contado 5",
            "",
            "Borrar NO pone el producto a 0: lo deja SIN CONTAR, y al contabilizar se quedará con lo que diga el sistema. Si lo que quiere decir es que no hay ninguna unidad, no la borre: cuéntela a 0.",
            "",
            "Borre solo una fila que sobra (duplicada o contada sin ubicación). ¿Borrarla?"}), texto)
    End Function

    <TestMethod()>
    Public Async Function NoEnLaConfirmacion_NoLlama() As Task
        Confirma(False)
        Dim dialogo = CrearDialogo(ConBloqueante("SinUbicacion", Fila(12)))

        Await dialogo.BorrarFila(dialogo.Bloqueantes.Single().Filas.Single())

        A.CallTo(Function() _servicio.BorrarFila(A(Of Integer).Ignored)).MustNotHaveHappened()
        A.CallTo(Function() _servicio.Comprobaciones(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function Conflicto409_EnsenaElMensajeYNoCierra() As Task
        A.CallTo(Function() _servicio.BorrarFila(12)).Throws(New ConflictoInventarioException("Esa fila ya está contabilizada (traspaso 77263)."))
        Dim dialogo = CrearDialogo(ConBloqueante("SinUbicacion", Fila(12)))
        Dim cerrada = False
        AddHandler dialogo.RequestClose, Sub(r) cerrada = True

        Await dialogo.BorrarFila(dialogo.Bloqueantes.Single().Filas.Single())

        A.CallTo(Sub() _dialogos.ShowError("Esa fila ya está contabilizada (traspaso 77263).")).MustHaveHappenedOnceExactly()
        A.CallTo(Function() _servicio.Comprobaciones(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored)).MustNotHaveHappened()
        Assert.IsFalse(cerrada)
        Assert.IsTrue(dialogo.HayBloqueantes)
        Assert.IsFalse(dialogo.EstaOcupado)
    End Function

    <TestMethod()>
    Public Async Function FilaYaNoExiste_AvisaYRefresca() As Task
        A.CallTo(Function() _servicio.BorrarFila(12)).Throws(New FilaInventarioNoEncontradaException("No existe la fila 12."))
        A.CallTo(Function() _servicio.Comprobaciones("1", "ALG", _dia)).Returns(Task.FromResult(SinNada()))
        Dim dialogo = CrearDialogo(ConBloqueante("SinUbicacion", Fila(12)))

        Await dialogo.BorrarFila(dialogo.Bloqueantes.Single().Filas.Single())

        A.CallTo(Sub() _dialogos.ShowError("No existe la fila 12.")).MustHaveHappenedOnceExactly()
        Assert.IsFalse(dialogo.HayBloqueantes)
    End Function

    <TestMethod()>
    Public Async Function LineaSinNumeroDeOrden_NoSePuedeBorrarAunqueLaApiDigaQueSi() As Task
        Dim dialogo = CrearDialogo(ConBloqueante("SinUbicacion", Fila(Nothing), Fila(0)))

        For Each f In dialogo.Bloqueantes.Single().Filas
            Assert.IsFalse(ComprobacionesInventarioDialogViewModel.EsBorrable(f))
            Assert.IsFalse(dialogo.BorrarFilaCommand.CanExecute(f))
            Await dialogo.BorrarFila(f)
        Next

        A.CallTo(Function() _dialogos.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored, A(Of Boolean).Ignored)).MustNotHaveHappened()
        A.CallTo(Function() _servicio.BorrarFila(A(Of Integer).Ignored)).MustNotHaveHappened()
    End Function

    ' --- Carlos 11/11/26: el botón lo decide la API (SePuedeBorrar por línea) ---

    Private Const JSON_COMPROBACIONES As String =
        "{""Almacen"":""ALG"",""Fecha"":""2026-11-21T00:00:00"",""Bloqueantes"":[{""Codigo"":""FilasDuplicadas"",""Titulo"":""Duplicadas"",""Filas"":[" +
        "{""NºOrden"":1,""Producto"":""17404""}," +
        "{""NºOrden"":2,""Producto"":""17404"",""SePuedeBorrar"":false}," +
        "{""NºOrden"":3,""Producto"":""17404"",""SePuedeBorrar"":true}]}]," +
        """Avisos"":[{""Codigo"":""FilasBorradas"",""Titulo"":""Filas borradas hoy del recuento"",""Filas"":[" +
        "{""Producto"":""17404"",""Descripcion"":""Champú"",""StockCalculado"":3,""StockReal"":0,""Usuario"":""Andre""}]}]}"

    <TestMethod()>
    Public Async Function Boton_SinCampoOFalse_NoSale_ConTrue_Sale() As Task
        Dim c = Newtonsoft.Json.JsonConvert.DeserializeObject(Of ComprobacionesInventarioDTO)(JSON_COMPROBACIONES)
        Dim dialogo = CrearDialogo(c)
        Dim filas = dialogo.Bloqueantes.Single().Filas

        Assert.IsFalse(filas(0).SePuedeBorrar, "Sin el campo (API de antes): sin botón")
        Assert.IsFalse(dialogo.BorrarFilaCommand.CanExecute(filas(0)))
        Assert.IsFalse(filas(1).SePuedeBorrar)
        Assert.IsFalse(dialogo.BorrarFilaCommand.CanExecute(filas(1)))
        Assert.IsTrue(filas(2).SePuedeBorrar)
        Assert.IsTrue(dialogo.BorrarFilaCommand.CanExecute(filas(2)))

        Await dialogo.BorrarFila(filas(0))
        Await dialogo.BorrarFila(filas(1))
        A.CallTo(Function() _servicio.BorrarFila(A(Of Integer).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Sub AvisoFilasBorradasHoy_SePintaComoUnAvisoSinBoton()
        Dim c = Newtonsoft.Json.JsonConvert.DeserializeObject(Of ComprobacionesInventarioDTO)(JSON_COMPROBACIONES)

        Dim dialogo = CrearDialogo(c)

        Assert.IsTrue(dialogo.HayAvisos)
        Dim aviso = dialogo.Avisos.Single()
        Assert.AreEqual("Filas borradas hoy del recuento (1 fila)", aviso.Cabecera)
        Assert.IsNull(aviso.Filas.Single().NumOrden)
        Assert.IsFalse(ComprobacionesInventarioDialogViewModel.EsBorrable(aviso.Filas.Single()))
    End Sub

    <TestMethod()>
    Public Sub Explicacion_ConFilasBorrables_DiceQueBorrarNoEsCero_YSinEllasNo()
        Dim conBoton = CrearDialogo(ConBloqueante("SinUbicacion", Fila(12)))
        Dim sinBoton = CrearDialogo(ConBloqueante("SinUbicacion", Fila(12, False)))

        StringAssert.Contains(conBoton.Explicacion, "Solo se pueden borrar las filas que sobran")
        StringAssert.Contains(conBoton.Explicacion, "cuéntela a 0")
        Assert.IsFalse(sinBoton.Explicacion.Contains("borrar"))
    End Sub

    ' --- Pantalla: tras borrar en la ventana, se contabiliza con las comprobaciones vueltas a pedir ---

    <TestMethod()>
    Public Async Function Pantalla_SinBloqueantesTrasBorrar_Contabiliza() As Task
        Dim configuracion = A.Fake(Of IConfiguracion)()
        A.CallTo(Function() configuracion.leerParametro(A(Of String).Ignored, Parametros.Claves.AlmacenInventario)).Returns(Task.FromResult("ALG"))
        A.CallTo(Function() configuracion.UsuarioEnGrupo(A(Of String).Ignored)).Returns(True)
        Dim iniciales = ConBloqueante("SinUbicacion", Fila(12))
        A.CallTo(Function() _servicio.Comprobaciones("1", "ALG", _dia)).Returns(Task.FromResult(iniciales))
        A.CallTo(Function() _dialogos.ShowDialogAsync(ComprobacionesInventarioDialogViewModel.NOMBRE, A(Of ParametrosDialogo).Ignored)) _
            .Returns(Task.FromResult(New ResultadoDialogo(ResultadoBoton.OK, New ParametrosDialogo From {
                {ComprobacionesInventarioDialogViewModel.PARAMETRO_CONFIRMAR_CONTADO_CERO, False},
                {ComprobacionesInventarioDialogViewModel.PARAMETRO_COMPROBACIONES, SinNada()}})))
        A.CallTo(Function() _servicio.Contabilizar(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored, A(Of Boolean).Ignored)) _
            .Returns(Task.FromResult(New ResultadoContabilizarInventario With {.Contabilizacion = New ContabilizacionInventarioDTO}))
        Dim vm As New InventarioViewModel(A.Fake(Of IServicioNavegacion)(), configuracion, _dialogos, _servicio)
        vm.fechaSeleccionada = _dia

        Await vm.ContabilizarInventario()

        A.CallTo(Function() _dialogos.ShowConfirmationAnswer("Contabilizar inventario",
            A(Of String).That.Contains("9 filas"), True)).MustHaveHappenedOnceExactly()
        A.CallTo(Function() _servicio.Contabilizar("1", "ALG", _dia, False)).MustHaveHappenedOnceExactly()
    End Function

    ' --- Servicio (HTTP falso) ---

    Private Class ManejadorFalso
        Inherits HttpMessageHandler

        Public ReadOnly Peticiones As New List(Of String)
        Public Property Respuesta As Func(Of HttpRequestMessage, HttpResponseMessage)

        Protected Overrides Function SendAsync(request As HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
            Peticiones.Add(request.Method.Method + " " + request.RequestUri.PathAndQuery)
            Return Task.FromResult(Respuesta.Invoke(request))
        End Function
    End Class

    Private _manejador As ManejadorFalso

    Private Function CrearServicio(estado As HttpStatusCode, cuerpo As String) As ServicioInventario
        _manejador = New ManejadorFalso With {
            .Respuesta = Function(r) New HttpResponseMessage(estado) With {.Content = New StringContent(If(cuerpo, String.Empty))}
        }
        Dim factoria = A.Fake(Of IClienteApiFactory)()
        A.CallTo(Function() factoria.Crear()).ReturnsLazily(Function() New HttpClient(_manejador, False) With {
            .BaseAddress = New Uri("http://localhost/api/")
        })
        Return New ServicioInventario(factoria)
    End Function

    <TestMethod()>
    Public Async Function Servicio_204_HaceElDelete() As Task
        Dim servicio = CrearServicio(HttpStatusCode.NoContent, Nothing)

        Await servicio.BorrarFila(948787)

        Assert.AreEqual("DELETE /api/Inventarios/Filas/948787", _manejador.Peticiones.Single())
    End Function

    <TestMethod()>
    Public Async Function Servicio_409_LanzaConElMessage() As Task
        Dim servicio = CrearServicio(HttpStatusCode.Conflict, "{""Message"":""Esa fila ya no está pendiente.""}")

        Dim ex = Await Assert.ThrowsExceptionAsync(Of ConflictoInventarioException)(Function() servicio.BorrarFila(1))

        Assert.AreEqual("Esa fila ya no está pendiente.", ex.Message)
    End Function

    <TestMethod()>
    Public Async Function Servicio_403_LanzaConElMessage() As Task
        Dim servicio = CrearServicio(HttpStatusCode.Forbidden, "{""Message"":""No tiene permiso para borrar filas del inventario.""}")

        Dim ex = Await Assert.ThrowsExceptionAsync(Of Exception)(Function() servicio.BorrarFila(1))

        Assert.AreEqual("No tiene permiso para borrar filas del inventario.", ex.Message)
    End Function

    <TestMethod()>
    Public Async Function Servicio_404DeLaFila_LanzaNoEncontrada() As Task
        Dim servicio = CrearServicio(HttpStatusCode.NotFound, "{""Message"":""No existe la fila 1.""}")

        Dim ex = Await Assert.ThrowsExceptionAsync(Of FilaInventarioNoEncontradaException)(Function() servicio.BorrarFila(1))

        Assert.AreEqual("No existe la fila 1.", ex.Message)
    End Function

    <TestMethod()>
    Public Async Function Servicio_404Vacio_LanzaNoEncontrada() As Task
        Dim servicio = CrearServicio(HttpStatusCode.NotFound, Nothing)

        Dim ex = Await Assert.ThrowsExceptionAsync(Of FilaInventarioNoEncontradaException)(Function() servicio.BorrarFila(1))

        Assert.AreEqual(ServicioInventario.MENSAJE_FILA_NO_ENCONTRADA, ex.Message)
    End Function

    <TestMethod()>
    Public Async Function Servicio_ServidorAntiguo_404DeRutaO405_MensajeClaro() As Task
        For Each respuesta In {
            (HttpStatusCode.NotFound, "{""Message"":""No HTTP resource was found that matches the request URI 'http://x/api/Inventarios/Filas/1'."",""MessageDetail"":""No action was found.""}"),
            (HttpStatusCode.NotFound, "<html><body>404</body></html>"),
            (HttpStatusCode.MethodNotAllowed, "{""Message"":""The requested resource does not support http method 'DELETE'.""}")}
            Dim servicio = CrearServicio(respuesta.Item1, respuesta.Item2)

            Dim ex = Await Assert.ThrowsExceptionAsync(Of Exception)(Function() servicio.BorrarFila(1))

            Assert.AreEqual("Esta opción necesita la versión nueva del servidor.", ex.Message)
        Next
    End Function
End Class
