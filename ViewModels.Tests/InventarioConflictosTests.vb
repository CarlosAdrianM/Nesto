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
''' NestoAPI#784 (09/11/26, el 42341 de Paloma en ALC): nada de filas duplicadas ni de volver a abrir sin querer lo ya
''' contabilizado. El GET que falla ya no se toma por «no hay fila» (así se multiplicaban), el POST con fila ya existente suma
''' a esa, rellenar lo ya contabilizado pide «¿Contar otra vez?» y RecontadaSinContar se contabiliza solo confirmando el 0.
''' </summary>
<TestClass()>
Public Class InventarioConflictosTests

    Private ReadOnly _dia As New Date(2026, 11, 21)

    ' --- Servicio (HTTP falso) ---

    Private Class ManejadorFalso
        Inherits HttpMessageHandler

        Public ReadOnly Peticiones As New List(Of String)
        Public ReadOnly Cuerpos As New List(Of String)
        Public Property Respuesta As Func(Of HttpRequestMessage, HttpResponseMessage)

        Protected Overrides Async Function SendAsync(request As HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
            Peticiones.Add(request.Method.Method + " " + request.RequestUri.PathAndQuery)
            Cuerpos.Add(If(request.Content Is Nothing, Nothing, Await request.Content.ReadAsStringAsync()))
            Return Respuesta.Invoke(request)
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

    Private Const CUERPO_DUPLICADAS As String =
        "{""Message"":""El 42341 tiene 2 filas sin contabilizar el 21/11/2026 en ALC. Hay que dejar solo una antes de seguir contando: avise a Informática."",""Codigo"":""FilasDuplicadas"",""PuedeContarOtraVez"":false," +
        """Lineas"":[{""NumeroOrden"":1,""Producto"":""42341"",""StockReal"":0},{""NumeroOrden"":2,""Producto"":""42341"",""StockReal"":2}]}"

    <TestMethod()>
    Public Async Function Servicio_BuscarLinea_404_EsQueNoHayFila() As Task
        Dim servicio = CrearServicio(HttpStatusCode.NotFound, "{""Message"":""No hay""}")

        Dim linea = Await servicio.BuscarLinea("1", "ALC", _dia, "42341")

        Assert.IsNull(linea)
    End Function

    <TestMethod()>
    Public Async Function Servicio_BuscarLinea_409FilasDuplicadas_LanzaConElMensajeDeLaApi() As Task
        Dim servicio = CrearServicio(HttpStatusCode.Conflict, CUERPO_DUPLICADAS)

        Dim ex = Await Assert.ThrowsExceptionAsync(Of ConflictoInventarioException)(Function() servicio.BuscarLinea("1", "ALC", _dia, "42341"))

        Assert.AreEqual(CodigosConflictoInventario.FILAS_DUPLICADAS, ex.Codigo)
        StringAssert.StartsWith(ex.Message, "El 42341 tiene 2 filas sin contabilizar")
        Assert.AreEqual(2, ex.Lineas.Count)
    End Function

    <TestMethod()>
    Public Async Function Servicio_BuscarLinea_500_LanzaYNoDiceQueNoHayFila() As Task
        Dim servicio = CrearServicio(HttpStatusCode.InternalServerError, "{""Message"":""An error has occurred.""}")

        Await Assert.ThrowsExceptionAsync(Of Exception)(Function() servicio.BuscarLinea("1", "ALC", _dia, "42341"))
    End Function

    <TestMethod()>
    Public Async Function Servicio_BuscarLinea_409Ambiguo_LanzaSinCodigo() As Task
        Dim servicio = CrearServicio(HttpStatusCode.Conflict, "{""Message"":""El código 8410 lo tienen varios productos"",""Productos"":[]}")

        Dim ex = Await Assert.ThrowsExceptionAsync(Of ConflictoInventarioException)(Function() servicio.BuscarLinea("1", "ALC", _dia, "8410"))

        Assert.IsNull(ex.Codigo)
        Assert.AreEqual("El código 8410 lo tienen varios productos", ex.Message)
    End Function

    <TestMethod()>
    Public Async Function Servicio_CrearLinea_409YaTieneFila_Lanza() As Task
        Dim servicio = CrearServicio(HttpStatusCode.Conflict,
            "{""Message"":""El 42341 ya tiene una fila sin contabilizar"",""Codigo"":""YaTieneFila"",""Lineas"":[{""NumeroOrden"":7,""Producto"":""42341""}]}")

        Dim ex = Await Assert.ThrowsExceptionAsync(Of ConflictoInventarioException)(
            Function() servicio.CrearLinea(New InventarioDTO With {.Empresa = "1", .Almacen = "ALC", .Fecha = _dia, .Producto = "42341", .StockReal = 1}))

        Assert.IsTrue(ex.EsCodigo(CodigosConflictoInventario.YA_TIENE_FILA))
        Assert.AreEqual(7, ex.Lineas.Single().NumeroOrden)
    End Function

    <TestMethod()>
    Public Async Function Servicio_Rellenar_409YaContabilizado_LanzaYaContabilizado() As Task
        Dim servicio = CrearServicio(HttpStatusCode.Conflict,
            "{""Message"":""Ya hay 64 filas de ALC contadas y contabilizadas hoy (Paloma: 64, traspaso 77263). Si de verdad hay que volver a contar, pulse «Contar otra vez»."",""Codigo"":""YaContabilizado"",""PuedeContarOtraVez"":true," +
            """Pendientes"":[],""Contabilizadas"":[{""Usuario"":""NUEVAVISION\\Paloma"",""Filas"":64,""Traspaso"":77263}]}")

        Dim ex = Await Assert.ThrowsExceptionAsync(Of InventarioYaContabilizadoException)(Function() servicio.Rellenar("1", "ALC", _dia, False))

        Assert.IsTrue(ex.PuedeContarOtraVez)
        Assert.AreEqual(77263, ex.Contabilizadas.Single().Traspaso)
        StringAssert.Contains(_manejador.Cuerpos.Single(), """OtraVez"":false")
    End Function

    <TestMethod()>
    Public Async Function Servicio_Rellenar_409SinCodigo_EsYaRellenado() As Task
        ' La API de antes de NestoAPI#784 no manda Codigo: ya había filas pendientes, se cuenta igual.
        Dim servicio = CrearServicio(HttpStatusCode.Conflict, "{""Message"":""Ya está rellenado"",""Pendientes"":[]}")

        Dim ex = Await Assert.ThrowsExceptionAsync(Of InventarioYaRellenadoException)(Function() servicio.Rellenar("1", "ALC", _dia, False))

        Assert.AreEqual("Ya está rellenado", ex.Message)
    End Function

    <TestMethod()>
    Public Async Function Servicio_Rellenar_OtraVez_LoMandaEnElCuerpo() As Task
        Dim servicio = CrearServicio(HttpStatusCode.OK, "{""Almacen"":""ALC"",""Filas"":3,""Unidades"":5}")

        Dim respuesta = Await servicio.Rellenar("1", "ALC", _dia, True)

        Assert.AreEqual(3, respuesta.Filas)
        StringAssert.Contains(_manejador.Cuerpos.Single(), """OtraVez"":true")
    End Function

    <TestMethod()>
    Public Async Function Servicio_Contabilizar_MandaConfirmarContadoCero() As Task
        Dim servicio = CrearServicio(HttpStatusCode.OK, "{""Almacen"":""ALC"",""Contabilizados"":[]}")

        Await servicio.Contabilizar("1", "ALC", _dia, True)

        StringAssert.Contains(_manejador.Cuerpos.Single(), """ConfirmarContadoCero"":true")
    End Function

    ' --- ViewModel ---

    Private _configuracion As IConfiguracion
    Private _dialogos As IServicioDialogos
    Private _servicio As IServicioInventario

    <TestInitialize()>
    Public Sub Inicializar()
        _configuracion = A.Fake(Of IConfiguracion)()
        _dialogos = A.Fake(Of IServicioDialogos)()
        _servicio = A.Fake(Of IServicioInventario)()
        A.CallTo(Function() _configuracion.leerParametro(A(Of String).Ignored, Parametros.Claves.AlmacenInventario)) _
            .Returns(Task.FromResult("ALC"))
        A.CallTo(Function() _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDAS)).Returns(True)
        A.CallTo(Function() _servicio.LeerPendientes(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored)) _
            .Returns(Task.FromResult(New List(Of InventarioPendienteDTO) From {New InventarioPendienteDTO With {.Filas = 64}}))
    End Sub

    Private Function CrearViewModel() As InventarioViewModel
        Dim vm As New InventarioViewModel(A.Fake(Of IServicioNavegacion)(), _configuracion, _dialogos, _servicio)
        vm.fechaSeleccionada = _dia
        Return vm
    End Function

    Private Shared Function Conflicto(codigo As String, mensaje As String) As ConflictoInventarioException
        Return New ConflictoInventarioException(mensaje, New ConflictoInventarioDTO With {.Codigo = codigo, .Message = mensaje})
    End Function

    Private Sub NoSeHaCreadoNiSumado()
        A.CallTo(Function() _servicio.CrearLinea(A(Of InventarioDTO).Ignored)).MustNotHaveHappened()
        A.CallTo(Function() _servicio.ActualizarLinea(A(Of InventarioDTO).Ignored, A(Of Boolean?).Ignored)).MustNotHaveHappened()
    End Sub

    <TestMethod()>
    Public Async Function Insertar_GetConFilasDuplicadas_EnsenaElMensajeYNoCreaOtra() As Task
        A.CallTo(Function() _servicio.BuscarLinea(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored, A(Of String).Ignored)) _
            .ThrowsAsync(Conflicto(CodigosConflictoInventario.FILAS_DUPLICADAS, "El 42341 tiene 2 filas sin contabilizar"))
        Dim vm = CrearViewModel()

        Dim movimiento = Await vm.InsertarProducto("42341")

        Assert.IsNull(movimiento)
        A.CallTo(Sub() _dialogos.ShowError("El 42341 tiene 2 filas sin contabilizar")).MustHaveHappenedOnceExactly()
        NoSeHaCreadoNiSumado()
    End Function

    <TestMethod()>
    Public Async Function Insertar_GetConVariasUbicaciones_EnsenaElMensajeYNoCreaOtra() As Task
        A.CallTo(Function() _servicio.BuscarLinea(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored, A(Of String).Ignored)) _
            .ThrowsAsync(Conflicto(CodigosConflictoInventario.VARIAS_UBICACIONES, "indique la ubicación"))
        Dim vm = CrearViewModel()

        Assert.IsNull(Await vm.InsertarProducto("42341"))

        A.CallTo(Sub() _dialogos.ShowError("indique la ubicación")).MustHaveHappenedOnceExactly()
        NoSeHaCreadoNiSumado()
    End Function

    <TestMethod()>
    Public Async Function Insertar_GetConOtroError_EnsenaElErrorYNoCrea() As Task
        A.CallTo(Function() _servicio.BuscarLinea(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored, A(Of String).Ignored)) _
            .ThrowsAsync(New Exception("An error has occurred."))
        Dim vm = CrearViewModel()

        Assert.IsNull(Await vm.InsertarProducto("42341"))

        A.CallTo(Sub() _dialogos.ShowError("An error has occurred.")).MustHaveHappenedOnceExactly()
        NoSeHaCreadoNiSumado()
    End Function

    <TestMethod()>
    Public Async Function Insertar_PostYaTieneFila_VuelveALeerlaYSumaAEsa() As Task
        Dim existente As New InventarioDTO With {.NumOrden = 7, .Producto = "42341     ", .StockReal = 2}
        Dim lecturas = 0
        A.CallTo(Function() _servicio.BuscarLinea("1", "ALC", _dia, "42341")) _
            .ReturnsLazily(Function()
                               lecturas += 1
                               Return Task.FromResult(If(lecturas = 1, Nothing, existente))
                           End Function)
        A.CallTo(Function() _servicio.CrearLinea(A(Of InventarioDTO).Ignored)) _
            .ThrowsAsync(Conflicto(CodigosConflictoInventario.YA_TIENE_FILA, "El 42341 ya tiene una fila sin contabilizar"))
        Dim vm = CrearViewModel()

        Dim movimiento = Await vm.InsertarProducto("42341")

        Assert.IsNotNull(movimiento)
        A.CallTo(Function() _servicio.ActualizarLinea(existente, False)).MustHaveHappenedOnceExactly()
        Assert.AreEqual(CShort(3), existente.StockReal)
        A.CallTo(Function() _servicio.CrearLinea(A(Of InventarioDTO).Ignored)).MustHaveHappenedOnceExactly()
        A.CallTo(Sub() _dialogos.ShowError(A(Of String).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function Insertar_PostYaTieneFila_YAlReleerNoEsta_EnsenaElMensaje() As Task
        A.CallTo(Function() _servicio.BuscarLinea(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored, A(Of String).Ignored)) _
            .Returns(Task.FromResult(Of InventarioDTO)(Nothing))
        A.CallTo(Function() _servicio.CrearLinea(A(Of InventarioDTO).Ignored)) _
            .ThrowsAsync(Conflicto(CodigosConflictoInventario.YA_TIENE_FILA, "El 42341 ya tiene una fila sin contabilizar"))
        Dim vm = CrearViewModel()

        Assert.IsNull(Await vm.InsertarProducto("42341"))

        A.CallTo(Sub() _dialogos.ShowError("El 42341 ya tiene una fila sin contabilizar")).MustHaveHappenedOnceExactly()
        A.CallTo(Function() _servicio.CrearLinea(A(Of InventarioDTO).Ignored)).MustHaveHappenedOnceExactly()
        A.CallTo(Function() _servicio.ActualizarLinea(A(Of InventarioDTO).Ignored, A(Of Boolean?).Ignored)).MustNotHaveHappened()
    End Function

    ' --- Rellenar lo ya contabilizado ---

    Private Sub SinPendientesYYaContabilizado()
        A.CallTo(Function() _servicio.LeerPendientes(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored)) _
            .Returns(Task.FromResult(New List(Of InventarioPendienteDTO)))
        A.CallTo(Function() _dialogos.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored)).Returns(True)
        A.CallTo(Function() _servicio.Rellenar("1", "ALC", _dia, False)) _
            .ThrowsAsync(New InventarioYaContabilizadoException("Ya hay 64 filas de ALC contadas y contabilizadas hoy (Paloma: 64, traspaso 77263)."))
        A.CallTo(Function() _servicio.Rellenar("1", "ALC", _dia, True)) _
            .Returns(Task.FromResult(New RellenarInventarioRespuestaDTO With {.Filas = 120, .Unidades = 345}))
        A.CallTo(Function() _servicio.BuscarLinea(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored, A(Of String).Ignored)) _
            .Returns(Task.FromResult(New InventarioDTO With {.NumOrden = 7, .Producto = "42341"}))
    End Sub

    <TestMethod()>
    Public Async Function Rellenar_YaContabilizado_PreguntaContarOtraVez_Si_RepiteConOtraVez() As Task
        SinPendientesYYaContabilizado()
        A.CallTo(Function() _dialogos.ShowConfirmationAnswer(InventarioViewModel.TITULO_CONTAR_OTRA_VEZ, A(Of String).Ignored, True)).Returns(True)
        Dim vm = CrearViewModel()

        Dim movimiento = Await vm.InsertarProducto("42341")

        A.CallTo(Function() _dialogos.ShowConfirmationAnswer(InventarioViewModel.TITULO_CONTAR_OTRA_VEZ,
            A(Of String).That.Matches(Function(m) m.StartsWith("Ya hay 64 filas de ALC") AndAlso m.EndsWith("¿Contar otra vez?")), True)) _
            .MustHaveHappenedOnceExactly()
        A.CallTo(Function() _servicio.Rellenar("1", "ALC", _dia, True)).MustHaveHappenedOnceExactly()
        A.CallTo(Sub() _dialogos.ShowNotification(A(Of String).Ignored, "Inventario rellenado: 120 productos, 345 unidades.")).MustHaveHappenedOnceExactly()
        Assert.IsNotNull(movimiento)
    End Function

    <TestMethod()>
    Public Async Function Rellenar_YaContabilizado_No_NiRellenaNiCuenta() As Task
        SinPendientesYYaContabilizado()
        A.CallTo(Function() _dialogos.ShowConfirmationAnswer(InventarioViewModel.TITULO_CONTAR_OTRA_VEZ, A(Of String).Ignored, True)).Returns(False)
        Dim vm = CrearViewModel()

        Dim movimiento = Await vm.InsertarProducto("42341")

        Assert.IsNull(movimiento)
        A.CallTo(Function() _servicio.Rellenar("1", "ALC", _dia, True)).MustNotHaveHappened()
        A.CallTo(Function() _servicio.BuscarLinea(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
        NoSeHaCreadoNiSumado()
        A.CallTo(Sub() _dialogos.ShowError(A(Of String).Ignored)).MustNotHaveHappened()
    End Function

    ' --- Contabilizar con RecontadaSinContar ---

    Private Shared Function Comprobacion(codigo As String, seConfirma As Boolean) As ComprobacionInventarioDTO
        Return New ComprobacionInventarioDTO With {
            .Codigo = codigo, .Titulo = codigo, .SeConfirma = seConfirma,
            .Filas = New List(Of FilaComprobacionInventarioDTO) From {
                New FilaComprobacionInventarioDTO With {.NumOrden = 12, .Producto = "42341", .StockCalculado = 2, .StockReal = 0}
            }
        }
    End Function

    Private Function Comprobaciones(ParamArray bloqueantes As ComprobacionInventarioDTO()) As ComprobacionesInventarioDTO
        Dim c As New ComprobacionesInventarioDTO With {
            .Almacen = "ALC", .Fecha = _dia,
            .Recuento = New List(Of RecuentoUsuarioInventarioDTO) From {New RecuentoUsuarioInventarioDTO With {.Usuario = "NUEVAVISION\Paloma", .Filas = 1}}
        }
        c.Bloqueantes.AddRange(bloqueantes)
        Return c
    End Function

    Private Sub PrepararContabilizar(comprobacionesDia As ComprobacionesInventarioDTO, confirmaEnElDialogo As Boolean)
        A.CallTo(Function() _servicio.Comprobaciones(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored)) _
            .Returns(Task.FromResult(comprobacionesDia))
        A.CallTo(Function() _servicio.Contabilizar(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored, A(Of Boolean).Ignored)) _
            .Returns(Task.FromResult(New ResultadoContabilizarInventario With {.Contabilizacion = New ContabilizacionInventarioDTO With {.Almacen = "ALC"}}))
        A.CallTo(Function() _dialogos.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored, A(Of Boolean).Ignored)).Returns(True)
        A.CallTo(Function() _dialogos.ShowDialogAsync(ComprobacionesInventarioDialogViewModel.NOMBRE, A(Of ParametrosDialogo).Ignored)) _
            .Returns(Task.FromResult(New ResultadoDialogo(ResultadoBoton.OK, New ParametrosDialogo From {
                {ComprobacionesInventarioDialogViewModel.PARAMETRO_CONFIRMAR_CONTADO_CERO, confirmaEnElDialogo}
            })))
    End Sub

    <TestMethod()>
    Public Async Function Contabilizar_RecontadaSinContarConfirmada_MandaConfirmarContadoCero() As Task
        PrepararContabilizar(Comprobaciones(Comprobacion(CodigosComprobacionInventario.RECONTADA_SIN_CONTAR, True)), True)
        Dim vm = CrearViewModel()

        Await vm.ContabilizarInventario()

        A.CallTo(Function() _servicio.Contabilizar("1", "ALC", _dia, True)).MustHaveHappenedOnceExactly()
        A.CallTo(Function() _dialogos.ShowConfirmationAnswer("Contabilizar inventario",
            A(Of String).That.Contains("su stock se quedará a cero"), True)).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function Contabilizar_RecontadaSinContarSinConfirmar_NoContabiliza() As Task
        PrepararContabilizar(Comprobaciones(Comprobacion(CodigosComprobacionInventario.RECONTADA_SIN_CONTAR, True)), False)
        Dim vm = CrearViewModel()

        Await vm.ContabilizarInventario()

        A.CallTo(Function() _servicio.Contabilizar(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored, A(Of Boolean).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function Contabilizar_FilasDuplicadas_NoContabilizaNiConfirmando() As Task
        PrepararContabilizar(Comprobaciones(Comprobacion(CodigosComprobacionInventario.RECONTADA_SIN_CONTAR, True),
                                            Comprobacion(CodigosComprobacionInventario.FILAS_DUPLICADAS, False)), True)
        Dim vm = CrearViewModel()

        Await vm.ContabilizarInventario()

        A.CallTo(Function() _servicio.Contabilizar(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored, A(Of Boolean).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function Contabilizar_SinBloqueantes_NoConfirmaContadoCero() As Task
        Dim c = Comprobaciones()
        c.Avisos.Add(Comprobacion(CodigosComprobacionInventario.CONTADO_CERO_CON_STOCK, False))
        PrepararContabilizar(c, False)
        Dim vm = CrearViewModel()

        Await vm.ContabilizarInventario()

        A.CallTo(Function() _servicio.Contabilizar("1", "ALC", _dia, False)).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function Contabilizar_409ConRecontadaSinContar_SiSeConfirmaEnElDialogoReintentaUnaVezConfirmando() As Task
        PrepararContabilizar(Comprobaciones(), True)
        Dim bloqueado = Comprobaciones(Comprobacion(CodigosComprobacionInventario.RECONTADA_SIN_CONTAR, True))
        A.CallTo(Function() _servicio.Contabilizar("1", "ALC", _dia, False)) _
            .Returns(Task.FromResult(New ResultadoContabilizarInventario With {.Bloqueado = bloqueado}))
        Dim vm = CrearViewModel()

        Await vm.ContabilizarInventario()

        A.CallTo(Function() _servicio.Contabilizar("1", "ALC", _dia, False)).MustHaveHappenedOnceExactly()
        A.CallTo(Function() _servicio.Contabilizar("1", "ALC", _dia, True)).MustHaveHappenedOnceExactly()
    End Function

    ' --- Diálogo ---

    <TestMethod()>
    Public Sub Dialogo_RecontadaSinContar_ContinuarSoloMarcandoLaCasillaYDevuelveLaConfirmacion()
        Dim dialogo As New ComprobacionesInventarioDialogViewModel(_servicio, _dialogos)
        Dim resultado As ResultadoDialogo = Nothing
        AddHandler dialogo.RequestClose, Sub(r) resultado = r

        dialogo.Cargar(Comprobaciones(Comprobacion(CodigosComprobacionInventario.RECONTADA_SIN_CONTAR, True)))

        Assert.IsTrue(dialogo.SePuedeConfirmarContadoCero)
        Assert.IsTrue(dialogo.MostrarContinuar)
        Assert.IsFalse(dialogo.ContinuarCommand.CanExecute(Nothing))
        StringAssert.Contains(dialogo.Explicacion, "He contado y de verdad hay 0")

        dialogo.ConfirmadoContadoCero = True
        Assert.IsTrue(dialogo.ContinuarCommand.CanExecute(Nothing))
        dialogo.ContinuarCommand.Execute(Nothing)

        Assert.AreEqual(ResultadoBoton.OK, resultado.Result)
        Assert.IsTrue(resultado.Parameters.GetValue(Of Boolean)(ComprobacionesInventarioDialogViewModel.PARAMETRO_CONFIRMAR_CONTADO_CERO))
    End Sub

    <TestMethod()>
    Public Sub Dialogo_FilasDuplicadas_NiCasillaNiContinuar()
        Dim dialogo As New ComprobacionesInventarioDialogViewModel(_servicio, _dialogos)

        dialogo.Cargar(Comprobaciones(Comprobacion(CodigosComprobacionInventario.RECONTADA_SIN_CONTAR, True),
                                      Comprobacion(CodigosComprobacionInventario.FILAS_DUPLICADAS, False)))
        dialogo.ConfirmadoContadoCero = True

        Assert.IsFalse(dialogo.SePuedeConfirmarContadoCero)
        Assert.IsFalse(dialogo.MostrarContinuar)
        Assert.IsFalse(dialogo.ContinuarCommand.CanExecute(Nothing))
    End Sub

    <TestMethod()>
    Public Sub Dialogo_SoloAvisos_ContinuarSinConfirmacion()
        Dim dialogo As New ComprobacionesInventarioDialogViewModel(_servicio, _dialogos)
        Dim resultado As ResultadoDialogo = Nothing
        AddHandler dialogo.RequestClose, Sub(r) resultado = r
        Dim c = Comprobaciones()
        c.Avisos.Add(Comprobacion(CodigosComprobacionInventario.CONTADO_CERO_CON_STOCK, False))

        dialogo.Cargar(c)
        dialogo.ContinuarCommand.Execute(Nothing)

        Assert.IsFalse(dialogo.SePuedeConfirmarContadoCero)
        Assert.AreEqual(ResultadoBoton.OK, resultado.Result)
        Assert.IsFalse(resultado.Parameters.GetValue(Of Boolean)(ComprobacionesInventarioDialogViewModel.PARAMETRO_CONFIRMAR_CONTADO_CERO))
    End Sub
End Class
