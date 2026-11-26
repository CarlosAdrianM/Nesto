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
    Private _configuracion As IConfiguracion

    <TestInitialize()>
    Public Sub Initialize()
        _servicio = A.Fake(Of IClienteComercialService)()
        _dialogos = A.Fake(Of IServicioDialogos)()
        _rapports = A.Fake(Of IRapportService)()
        _configuracion = A.Fake(Of IConfiguracion)()
        A.CallTo(Function() _configuracion.usuario).Returns("NUEVAVISION\Carlos")
        ' Sin esto, los pedidos piden el vendedor a MainViewModel (que en los tests no tiene API)
        A.CallTo(Function() _configuracion.leerParametro(A(Of String).Ignored, Nesto.Infrastructure.Shared.Parametros.Claves.PermitirVerClientesTodosLosVendedores)).Returns(Task.FromResult("1"))
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
        Return New ClienteComercialViewModel(_configuracion, _dialogos, _servicio, _rapports, _autenticacion) With {
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

    ' Hallazgo 5: Guardar mandaba estado, vendedor, país y vendedores por grupo tal como se cargaron, y pisaba lo que otro
    ' usuario hubiera cambiado entre medias. Ahora solo va lo cambiado (null = la API no lo toca).
    <TestMethod()>
    Public Async Function H5_AlGuardar_SoloVaLoQueHaCambiadoElUsuario() As Task
        Dim vm = Await ConFicha(Ficha())
        vm.clienteActivo.estado = 5 ' solo el estado

        Await vm.GuardarFichaComercialAsync()

        Dim put As JObject = _api.Puts().Single()
        Assert.AreEqual(5, CInt(put("estado")))
        Assert.AreEqual(JTokenType.Null, put("vendedor").Type, "El vendedor no se ha tocado: no se manda")
        Assert.AreEqual(JTokenType.Null, put("pais").Type)
        Assert.AreEqual(JTokenType.Null, put("VendedoresGrupoProducto").Type, "Peluquería no se ha tocado: la API no la toca")
        Assert.IsNull(put("PersonasContacto"))
    End Function

    <TestMethod()>
    Public Async Function H5_SoloElVendedor_NoMandaElEstadoCargado_YTrasGuardarSeMideDesdeLoGuardado() As Task
        Dim vm = Await ConFicha(Ficha())
        vm.clienteServidor.vendedor = "PA"

        Await vm.GuardarFichaComercialAsync()
        vm.clienteServidor.pais = "pt"
        Await vm.GuardarFichaComercialAsync()

        Dim puts = _api.Puts()
        Assert.AreEqual(2, puts.Count)
        Assert.AreEqual(JTokenType.Null, puts(0)("estado").Type, "El estado cargado no viaja: si otro lo ha cambiado, no se le pisa")
        Assert.AreEqual("PA", puts(0)("vendedor").ToString())
        Assert.AreEqual(JTokenType.Null, puts(1)("vendedor").Type, "El vendedor ya guardado no se vuelve a mandar")
        Assert.AreEqual("pt", puts(1)("pais").ToString())
    End Function

    <TestMethod()>
    Public Async Function H5_SinCambios_NoSeLlamaALaApi() As Task
        Dim vm = Await ConFicha(Ficha())
        vm.clienteServidor.vendedor = "je " ' el mismo, con relleno y en minúsculas

        Await vm.GuardarFichaComercialAsync()

        Assert.AreEqual(0, _api.Peticiones.Count)
        A.CallTo(Sub() _dialogos.ShowNotification("Guardar", "No ha cambiado nada de la ficha: no hay nada que guardar.")).MustHaveHappenedOnceExactly()
    End Function

    ' Hallazgo 6: el estado de peluquería de un cliente sin fila se perdía (la API no crea la fila sin vendedor) con
    ' «guardado correctamente».
    <TestMethod()>
    Public Async Function H6_EstadoDePeluqueriaSinVendedor_NoSeGuardaYSeDice() As Task
        Dim vm = Await ConFicha(Ficha(conPeluqueria:=False))
        vm.estadoPeluqueria = 3

        Await vm.GuardarFichaComercialAsync()

        Assert.AreEqual(0, _api.Peticiones.Count)
        A.CallTo(Sub() _dialogos.ShowError(GuardadoFichaComercial.MOTIVO_ESTADO_PELUQUERIA_SIN_VENDEDOR)).MustHaveHappenedOnceExactly()
        A.CallTo(Sub() _dialogos.ShowDialog("NotificationDialog", A(Of ParametrosDialogo).Ignored, A(Of Action(Of ResultadoDialogo)).Ignored)).MustNotHaveHappened()
    End Function

    ' Hallazgo 6: con vendedor, la API crea la fila con el estado 0 sin mirar el elegido: un segundo PUT, ya con la fila, lo pone.
    <TestMethod()>
    Public Async Function H6_FilaNuevaDePeluqueriaConEstado_SeGuardaElEstadoElegido() As Task
        Dim vm = Await ConFicha(Ficha(conPeluqueria:=False))
        vm.vendedorPorGrupo = "JE"
        vm.estadoPeluqueria = 3

        Await vm.GuardarFichaComercialAsync()

        Dim puts = _api.Puts()
        Assert.AreEqual(2, puts.Count)
        Dim fila As JObject = CType(puts(0)("VendedoresGrupoProducto")(0), JObject)
        Assert.AreEqual("PEL", fila("grupoProducto").ToString())
        Assert.AreEqual("JE", fila("vendedor").ToString())
        Dim segunda As JObject = CType(puts(1)("VendedoresGrupoProducto")(0), JObject)
        Assert.AreEqual(3, CInt(segunda("estado")))
        Assert.AreEqual(JTokenType.Null, puts(1)("estado").Type, "El segundo solo lleva peluquería")
        A.CallTo(Sub() _dialogos.ShowDialog("NotificationDialog", A(Of ParametrosDialogo).Ignored, A(Of Action(Of ResultadoDialogo)).Ignored)).MustHaveHappenedOnceExactly()
    End Function

    ' Hallazgo 6: si la ficha no traía la lista de vendedores por grupo, el estado tecleado no llegaba a ningún sitio.
    <TestMethod()>
    Public Async Function H6_FichaSinListaDeGrupos_ElEstadoDePeluqueriaTambienViaja() As Task
        Dim sinLista As ClienteJson = Ficha(conPeluqueria:=False)
        sinLista.VendedoresGrupoProducto = Nothing
        Dim vm = Await ConFicha(sinLista)
        vm.vendedorPorGrupo = "JE"

        Await vm.GuardarFichaComercialAsync()

        Dim fila As JObject = CType(_api.Puts().First()("VendedoresGrupoProducto")(0), JObject)
        Assert.AreEqual("JE", fila("vendedor").ToString())
        Assert.AreEqual(1, _api.Puts().Count, "Con el estado 0 basta el primero")
    End Function

    ''' <summary>La pestaña Deudas cargada con un efecto (nº orden 4711) sin estado, y elegido para editarlo.</summary>
    Private Async Function ConEfectoElegido() As Task(Of ClienteComercialViewModel)
        Dim vm = Await ConFicha(Ficha())
        _api.Respuesta = Function(r) Task.FromResult(New HttpResponseMessage(HttpStatusCode.OK) With {
            .Content = New StringContent("[{""Id"":4711,""Empresa"":""1"",""Cliente"":""15191"",""Contacto"":""0"",""Estado"":null,""FormaPago"":""RCB"",""Vencimiento"":""2026-12-01T00:00:00"",""ImportePendiente"":10.0}]")
        })
        Await vm.CargarDeudasAsync()
        vm.DeudaSeleccionada = vm.ListaDeudas.Single()
        vm.MotivoCambioEstado = "Lo pide el cliente"
        Return vm
    End Function

    ' Hallazgo 9: el seguimiento se creaba ANTES del PUT; si el PUT fallaba, quedaba apuntado un cambio que no se hizo.
    <TestMethod()>
    Public Async Function H9_SiElEfectoNoSeGuarda_NoSeApuntaElSeguimiento() As Task
        Dim vm = Await ConEfectoElegido()
        vm.DeudaSeleccionada.Estado = "RETENIDO"
        A.CallTo(Function() _servicio.ModificarExtractoCliente(A(Of ExtractoClienteDTO).Ignored)).Throws(New Exception("Solo Administración, Dirección o Informática pueden modificar los efectos del extracto del cliente."))

        Await vm.GuardarEfectoDeudaAsync()

        A.CallTo(Function() _rapports.crearRapport(A(Of SeguimientoClienteDTO).Ignored)).MustNotHaveHappened()
        A.CallTo(Sub() _dialogos.ShowError("No se ha modificado el efecto 4711: Solo Administración, Dirección o Informática pueden modificar los efectos del extracto del cliente.")).MustHaveHappenedOnceExactly()
    End Function

    ' Hallazgo 9: el texto decía siempre «cambió el estado»; ahora dice lo que ha cambiado, y va después del PUT.
    <TestMethod()>
    Public Async Function H9_ElSeguimientoVaDespuesDelPut_YDiceLoQueHaCambiado() As Task
        Dim vm = Await ConEfectoElegido()
        vm.DeudaSeleccionada.Vencimiento = New Date(2026, 12, 15)
        Dim comentario As String = Nothing
        A.CallTo(Function() _rapports.crearRapport(A(Of SeguimientoClienteDTO).Ignored)) _
            .Invokes(Sub(s As SeguimientoClienteDTO) comentario = s.Comentarios).Returns(Task.FromResult("ok"))

        Await vm.GuardarEfectoDeudaAsync()

        A.CallTo(Function() _servicio.ModificarExtractoCliente(vm.DeudaSeleccionada)).MustHaveHappenedOnceExactly() _
            .Then(A.CallTo(Function() _rapports.crearRapport(A(Of SeguimientoClienteDTO).Ignored)).MustHaveHappenedOnceExactly())
        Assert.AreEqual("Carlos modificó el extracto de cliente con nº orden 4711 (vencimiento del 01/12/2026 al 15/12/2026) dando como motivo: Lo pide el cliente", comentario)
    End Function

    <TestMethod()>
    Public Sub H9_TextoDelSeguimiento_ComparaComoLaApi()
        Dim antes As New ExtractoClienteDTO With {.Id = 1, .Estado = Nothing, .CCC = "1  ", .FormaPago = "rcb", .Vencimiento = New Date(2026, 12, 1)}
        Dim despues As New ExtractoClienteDTO With {.Id = 1, .Estado = "retenido", .CCC = "1", .FormaPago = "RCB", .Vencimiento = New Date(2026, 12, 1)}

        Assert.AreEqual("Ana modificó el extracto de cliente con nº orden 1 (estado de «en blanco» a «RETENIDO») dando como motivo: x",
                        ClienteComercialViewModel.TextoSeguimientoCambioEfecto("Ana", antes, despues, " x "))
    End Sub

    ' Hallazgo 10: ante un 403/500 la lista quedaba a Nothing sin decir nada (parecía «no tiene facturas») o reventaba.
    <TestMethod()>
    Public Async Function H10_SiLasFacturasNoSePuedenCargar_SeDicePorQue() As Task
        Dim vm = Await ConFicha(Ficha())
        _api.Respuesta = Function(r) Task.FromResult(New HttpResponseMessage(HttpStatusCode.Forbidden) With {
            .Content = New StringContent("{""Message"":""No tiene permiso para ver las facturas de este cliente.""}")
        })

        Await vm.CargarFacturasAsync()

        Assert.IsNull(vm.ListaFacturas)
        Assert.AreEqual("No se han podido cargar las facturas del cliente 15191: No tiene permiso para ver las facturas de este cliente.", vm.mensajeError)
    End Function

    <TestMethod()>
    Public Async Function H10_SiLaRedFallaAlCargarPedidos_NoRevientaYSeDice() As Task
        Dim vm = Await ConFicha(Ficha())
        _api.Respuesta = Function(r) Task.FromException(Of HttpResponseMessage)(New HttpRequestException("No se puede conectar con el servidor remoto"))

        Await vm.CargarPedidosAsync()

        Assert.IsNull(vm.ListaPedidos)
        StringAssert.Contains(vm.mensajeError, "No se han podido cargar los pedidos del cliente 15191: No se puede conectar con el servidor remoto")
    End Function

    <TestMethod()>
    Public Async Function H10_DeudasConRespuestaVacia_NoRevientan() As Task
        Dim vm = Await ConFicha(Ficha())
        _api.Respuesta = Function(r) Task.FromResult(New HttpResponseMessage(HttpStatusCode.OK) With {.Content = New StringContent("")})

        Await vm.CargarDeudasAsync()

        Assert.IsNotNull(vm.ListaDeudas)
        Assert.AreEqual(0, vm.ListaDeudas.Count)
    End Function

    <TestMethod()>
    Public Async Function H10_DeudaVencidaQueNoSePuedeLeer_NoSaleComoCeroEuros_YElAvisoSeVaConOtroCliente() As Task
        Dim vm = Await ConFicha(Ficha())
        _api.Respuesta = Function(r) Task.FromResult(New HttpResponseMessage(HttpStatusCode.InternalServerError) With {.Content = New StringContent("")})

        Await vm.CargarDeudaVencidaAsync()

        Assert.IsFalse(vm.deudaVencida.HasValue, "Ni 0 € ni nada: no se sabe")
        Assert.AreEqual("No se ha podido leer la deuda vencida del cliente 15191: el servidor ha contestado 500 (Internal Server Error).", vm.mensajeError)

        Dim otro As ClienteJson = Ficha()
        otro.cliente = "30676"
        vm.clienteActivo = otro
        Assert.IsTrue(String.IsNullOrEmpty(vm.mensajeError), "El aviso era del cliente anterior")
    End Function

    ' Hallazgo 12: el filtro iba a la URL sin escapar y una búsqueda vieja que contestaba tarde pisaba la nueva.
    <TestMethod()>
    Public Async Function H12_ElFiltroVaEscapado_YUnaBusquedaViejaNoPisaLaNueva() As Task
        Dim vm = Comercial()
        vm.ListaClientesFiltrable = New Nesto.Infrastructure.Shared.ColeccionFiltrable()
        Dim lenta As New TaskCompletionSource(Of HttpResponseMessage)()
        _api.Respuesta = Function(r)
                             If r.RequestUri.Query.Contains("filtro=garc&") OrElse r.RequestUri.Query.EndsWith("filtro=garc") Then
                                 Return lenta.Task
                             End If
                             Return Task.FromResult(New HttpResponseMessage(HttpStatusCode.OK) With {
                                 .Content = New StringContent("[{""empresa"":""1"",""cliente"":""2"",""contacto"":""0"",""nombre"":""GARCÍA & HIJOS""}]")})
                         End Function

        Dim vieja As Task = vm.BuscarClientesAsync("garc")
        Await vm.BuscarClientesAsync("garcía & hijos")
        lenta.SetResult(New HttpResponseMessage(HttpStatusCode.OK) With {
            .Content = New StringContent("[{""empresa"":""1"",""cliente"":""1"",""contacto"":""0"",""nombre"":""GARCERÁN""}]")})
        Await vieja

        Assert.AreEqual("2", CType(vm.ListaClientesFiltrable.ListaOriginal.Single(), ClienteJson).cliente, "Se queda la última búsqueda")
        Assert.IsFalse(vm.estaOcupado)
        StringAssert.Contains(_api.Peticiones.Last().Url, "filtro=garc%C3%ADa%20%26%20hijos")
    End Function

    ''' <summary>La reclamación de deuda con el motor Paygold, lista para mandar.</summary>
    Private Function ConReclamacionPaygold(respuestaP2F As String) As ClienteComercialViewModel
        Dim vm = Comercial()
        vm.clienteActivo = Ficha()
        vm.MotorPagosSeleccionado = "Paygold"
        vm.ImporteReclamarDeuda = 25D
        vm.AsuntoReclamarDeuda = "Pago pedido 123456"
        vm.CorreoReclamarDeuda = "cliente@example.com"
        vm.RevisorConceptoPago = A.Fake(Of ControlesUsuario.Services.IRevisorConceptoPago)()
        A.CallTo(Function() vm.RevisorConceptoPago.ElegirConcepto(A(Of String).Ignored, A(Of String).Ignored, A(Of String).Ignored)) _
            .ReturnsLazily(Function(concepto As String, empresa As String, cliente As String) Task.FromResult(concepto))
        _api.Respuesta = Function(r)
                             Dim cuerpo As String = If(r.RequestUri.AbsolutePath.EndsWith("/Pagos"),
                                 "{""UrlPaginaPago"":""https://api.nuevavision.es/pago/abc""}", respuestaP2F)
                             Return Task.FromResult(New HttpResponseMessage(HttpStatusCode.OK) With {.Content = New StringContent(cuerpo)})
                         End Function
        Return vm
    End Function

    ' Hallazgo 14: con Paygold, POST Pagos mandaba por correo el enlace de nuestra página y ReclamacionDeuda otro de Paygold:
    ' dos enlaces distintos para la misma deuda.
    <TestMethod()>
    Public Async Function H14_ConPaygold_AlClienteSoloLeLlegaElEnlaceDePaygold() As Task
        Dim vm = ConReclamacionPaygold("{""TramitadoOK"":true,""Enlace"":""https://paygold.example/x""}")

        Await vm.ReclamarDeudaAsync()

        Dim pagos As JObject = JObject.Parse(_api.Peticiones.First(Function(p) p.Url.EndsWith("/Pagos")).Cuerpo)
        Assert.AreEqual(JTokenType.Null, pagos("Correo").Type, "POST Pagos no manda su correo: el enlace lo manda Paygold")
        Dim p2f As JObject = JObject.Parse(_api.Peticiones.First(Function(p) p.Url.EndsWith("/ReclamacionDeuda")).Cuerpo)
        Assert.AreEqual("cliente@example.com", p2f("Correo").ToString())
        Assert.AreEqual("https://paygold.example/x", vm.EnlaceReclamarDeuda)
    End Function

    <TestMethod()>
    Public Async Function H14_SiPaygoldNoLoTramita_SeDiceQueNoLeHaLlegadoNada() As Task
        Dim vm = ConReclamacionPaygold("{""TramitadoOK"":false}")
        Dim mensaje As String = Nothing
        A.CallTo(Sub() _dialogos.ShowDialog("NotificationDialog", A(Of ParametrosDialogo).Ignored, A(Of Action(Of ResultadoDialogo)).Ignored)) _
            .Invokes(Sub(nombre As String, parametros As ParametrosDialogo, callback As Action(Of ResultadoDialogo)) mensaje = parametros.GetValue(Of String)("message"))

        Await vm.ReclamarDeudaAsync()

        StringAssert.Contains(mensaje, "Paygold no ha enviado el enlace al cliente (Paygold no lo ha tramitado): no le ha llegado nada.")
        StringAssert.Contains(mensaje, "https://api.nuevavision.es/pago/abc")
        Assert.AreEqual("https://api.nuevavision.es/pago/abc", vm.EnlaceReclamarDeuda)
    End Function

    <TestMethod()>
    Public Async Function H14_ConNestoPago_ElCorreoSigueYendoEnPagos() As Task
        Dim vm = ConReclamacionPaygold("{}")
        vm.MotorPagosSeleccionado = "NestoPago"

        Await vm.ReclamarDeudaAsync()

        Assert.AreEqual("cliente@example.com", JObject.Parse(_api.Peticiones.Single().Cuerpo)("Correo").ToString())
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

    ' Hallazgo 5 (parte de Nesto, contrato de NestoAPI-901): con el PUT va el «original», lo que se cargó.
    <TestMethod()>
    Public Async Function H5_ElPutLlevaElOriginalConLoCargado() As Task
        Dim vm = Await ConFicha(Ficha())
        vm.clienteServidor.vendedor = "PA"

        Await vm.GuardarFichaComercialAsync()

        Dim original As JObject = CType(_api.Puts().Single()("original"), JObject)
        Assert.AreEqual(0, CInt(original("estado")))
        Assert.AreEqual("JE", original("vendedor").ToString(), "El vendedor cargado, no el tecleado")
        Assert.AreEqual("ES", original("pais").ToString())
        Assert.AreEqual("JE", original("vendedorPeluqueria").ToString())
        Assert.AreEqual(0, CInt(original("estadoPeluqueria")))
    End Function

    <TestMethod()>
    Public Async Function H5_SinFilaDePeluqueria_ElOriginalVaSinVendedorDePeluqueria_YElSegundoPutSinOriginal() As Task
        Dim vm = Await ConFicha(Ficha(conPeluqueria:=False))
        vm.vendedorPorGrupo = "JE"
        vm.estadoPeluqueria = 3

        Await vm.GuardarFichaComercialAsync()

        Dim puts = _api.Puts()
        Assert.AreEqual(JTokenType.Null, puts(0)("original")("vendedorPeluqueria").Type, "Sin fila: la API lo compara con «sin vendedor»")
        Assert.IsNull(puts(1)("original"), "La fila la acaba de crear el primer PUT: el segundo no puede chocar consigo mismo")
    End Function

    Private Shared Function RespuestaClienteCambiado(ParamArray campos As String()) As HttpResponseMessage
        Dim cuerpo As New JObject(
            New JProperty("Message", "La ficha del cliente 15191 la ha cambiado Aida a las 15:14 desde que usted la abrió (estado). Recárguela y vuelva a hacer los cambios."),
            New JProperty("Codigo", "CLIENTE_CAMBIADO"),
            New JProperty("details", New JObject(New JProperty("campos", New JArray(campos)), New JProperty("usuario", "Aida"))))
        Return New HttpResponseMessage(HttpStatusCode.Conflict) With {.Content = New StringContent(cuerpo.ToString())}
    End Function

    ' Hallazgo 5: el 409 CLIENTE_CAMBIADO enseña el mensaje y, al recargar, lo que no choca se conserva y lo que choca no.
    <TestMethod()>
    Public Async Function H5_ConflictoAlGuardar_SeRecarga_YSeConservaLoQueNoChoca() As Task
        Dim nueva As ClienteJson = Ficha()
        nueva.estado = 7 ' lo que guardó Aida
        A.CallTo(Function() _servicio.LeerCliente("1", "15191", "0")).ReturnsNextFromSequence(Ficha(), nueva)
        Dim vm = Comercial()
        Await vm.ActualizarClienteAsync("1", "15191", "0")
        vm.cargarVendedoresPorGrupo()
        vm.clienteActivo.estado = 5
        vm.clienteServidor.pais = "PT"
        _api.Respuesta = Function(r) Task.FromResult(RespuestaClienteCambiado("estado"))

        Await vm.GuardarFichaComercialAsync()

        A.CallTo(Sub() _dialogos.ShowDialog("ConfirmationDialog",
            A(Of ParametrosDialogo).That.Matches(Function(p) p.GetValue(Of String)("message").StartsWith("La ficha del cliente 15191 la ha cambiado Aida")),
            A(Of Action(Of ResultadoDialogo)).Ignored)).MustHaveHappenedOnceExactly()
        Assert.AreEqual(7, vm.clienteActivo.estado, "En lo que choca se queda lo del otro usuario")
        Assert.AreEqual("PT", vm.clienteServidor.pais, "Lo que no choca se vuelve a poner encima")
        A.CallTo(Sub() _dialogos.ShowNotification("Ficha recargada", A(Of String).That.Contains("Se mantienen sus cambios en: país."))).MustHaveHappenedOnceExactly()
        A.CallTo(Sub() _dialogos.ShowDialog("NotificationDialog", A(Of ParametrosDialogo).Ignored, A(Of Action(Of ResultadoDialogo)).Ignored)).MustNotHaveHappened()
        A.CallTo(Sub() _dialogos.ShowError(A(Of String).Ignored)).MustNotHaveHappened()

        ' Y el siguiente guardado se mide desde lo recargado: solo el país, con el original nuevo
        _api.Respuesta = Function(r) Task.FromResult(New HttpResponseMessage(HttpStatusCode.NoContent))
        Await vm.GuardarFichaComercialAsync()
        Dim ultimo As JObject = _api.Puts().Last()
        Assert.AreEqual(JTokenType.Null, ultimo("estado").Type)
        Assert.AreEqual("PT", ultimo("pais").ToString())
        Assert.AreEqual(7, CInt(ultimo("original")("estado")))
    End Function

    <TestMethod()>
    Public Sub H5_CamposEnConflicto_SoloConElCodigoDeLaApi()
        Assert.IsNull(GuardadoFichaComercial.CamposEnConflicto(HttpStatusCode.Conflict, "{""Message"":""otro 409""}"))
        Assert.IsNull(GuardadoFichaComercial.CamposEnConflicto(HttpStatusCode.BadRequest, "{""Codigo"":""CLIENTE_CAMBIADO""}"))
        Assert.IsNull(GuardadoFichaComercial.CamposEnConflicto(HttpStatusCode.Conflict, "no es json"))
        CollectionAssert.AreEqual({"estado", "pais"}, GuardadoFichaComercial.CamposEnConflicto(HttpStatusCode.Conflict,
            "{""Codigo"":""CLIENTE_CAMBIADO"",""details"":{""campos"":[""estado"",""pais""]}}").ToArray())
    End Sub

    <TestMethod()>
    Public Sub H5_CamposAReaplicar_LaPeluqueriaVaJunta()
        Dim reaplicar = GuardadoFichaComercial.CamposAReaplicar({"vendedor", "vendedorPeluqueria", "estadoPeluqueria"}, {"estadoPeluqueria"})
        CollectionAssert.AreEqual({"vendedor"}, reaplicar.ToArray())
    End Sub

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
