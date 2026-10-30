Imports FakeItEasy
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Models
Imports Nesto.Infrastructure.Services
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models.Nesto.Models
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.ViewModels
Imports System.Collections.ObjectModel
Imports System.Threading.Tasks

''' <summary>
''' Nesto#576 (NestoAPI#750): la agencia del envío de un pedido la propone NestoAPI (GET api/EnviosAgencias/Propuesta) y Agencias
''' la lee en vez de calcularla. 30/10/26 (envío 250375, pedido 929440 de la ruta AT): la API proponía el reparto propio (14) y
''' Nesto grababa CTT (13) con su servicio 48, y el mozo lo cambiaba a mano. Si la API falla, lo de antes (el comparador) y un
''' aviso en la barra de mensajes.
''' </summary>
<TestClass()>
Public Class AgenciasViewModelPropuestaEnvioTests
    Private Const ASM As Integer = 1
    Private Const CTT As Integer = 13
    Private Const REPARTO_PROPIO As Integer = Constantes.Agencias.AGENCIA_REPARTO_PROPIO
    Private Const PEDIDO As Integer = 929440
    Private Const CP As String = "28001"

    Private servicio As IAgenciaService
    Private configuracion As IConfiguracion
    Private dialogService As IServicioDialogos
    Private comparador As IServicioComparadorAgencias
    Private respuestaComparador As TaskCompletionSource(Of OpcionEnvioAgencia)
    Private respuestaPropuesta As TaskCompletionSource(Of PropuestaEnvioDto)
    Private viewModel As AgenciasViewModel

    <TestInitialize()>
    Public Sub Initialize()
        servicio = A.Fake(Of IAgenciaService)
        configuracion = A.Fake(Of IConfiguracion)
        dialogService = A.Fake(Of IServicioDialogos)
        comparador = A.Fake(Of IServicioComparadorAgencias)
        ' El servidor contesta TARDE, como en la vida real: la selección local va primero.
        respuestaComparador = New TaskCompletionSource(Of OpcionEnvioAgencia)()
        A.CallTo(Function() comparador.MasEconomica(A(Of String).Ignored, A(Of String).Ignored, A(Of Decimal).Ignored, A(Of Decimal).Ignored, A(Of String).Ignored)) _
            .ReturnsLazily(Function() respuestaComparador.Task)
        respuestaPropuesta = New TaskCompletionSource(Of PropuestaEnvioDto)()
        A.CallTo(Function() servicio.LeerPropuestaEnvio(A(Of String).Ignored, A(Of Integer).Ignored, A(Of Decimal).Ignored)) _
            .ReturnsLazily(Function() respuestaPropuesta.Task)

        A.CallTo(Function() configuracion.leerParametro("1", "EmpresaPorDefecto")).Returns("1  ")
        A.CallTo(Function() configuracion.leerParametro(A(Of String).Ignored, "UltNumPedidoVta")).Returns("1")

        Dim empresa = A.Fake(Of Empresas)
        empresa.Número = "1  "
        A.CallTo(Function() servicio.CargarListaEmpresas()).Returns(New ObservableCollection(Of Empresas) From {empresa})
        A.CallTo(Function() servicio.CargarListaAgencias(A(Of String).Ignored)).ReturnsLazily(
            Function() New ObservableCollection(Of AgenciasTransporte) From {
                Agencia(ASM, "ASM"), Agencia(CTT, "CTT"), Agencia(REPARTO_PROPIO, Constantes.Agencias.NOMBRE_AGENCIA_REPARTO_PROPIO)})

        Dim pedidoAgencia = New PedidoAgenciaModel With {
            .Empresa = "1  ", .Número = PEDIDO, .Nº_Cliente = "40198", .Contacto = "0", .Vendedor = "MAN",
            .Clientes = New ClienteAgenciaModel With {.Nombre = "CLIENTE", .Dirección = "CALLE", .Población = "MADRID", .Provincia = "MADRID", .CodPostal = CP, .Teléfono = "911234567"}
        }
        A.CallTo(Function() servicio.LeerPedidoParaAgencia(A(Of String).Ignored, PEDIDO)).Returns(pedidoAgencia)
        A.CallTo(Function() servicio.LeerPedidoParaAgenciaPorNumero(PEDIDO, A(Of Boolean).Ignored)).Returns(pedidoAgencia)
        A.CallTo(Function() servicio.CargarListaEnviosPedido(A(Of String).Ignored, PEDIDO)).Returns(New ObservableCollection(Of EnviosAgencia))
        A.CallTo(Function() servicio.CargarEnvio(A(Of String).Ignored, PEDIDO)).Returns(Nothing)
    End Sub

    Private Shared Function Agencia(numero As Integer, nombre As String) As AgenciasTransporte
        Return New AgenciasTransporte With {.Empresa = "1  ", .Numero = numero, .Nombre = nombre}
    End Function

    Private Shared Function Propuesta(agencia As Integer, Optional servicio As Short = 0, Optional codPostal As String = CP,
                                      Optional origen As String = ContratoEnvioDesdePedido.ORIGEN_NUEVO, Optional coste As Decimal = 0D) As PropuestaEnvioDto
        Return New PropuestaEnvioDto With {.Empresa = "1", .Pedido = PEDIDO, .Agencia = agencia, .Servicio = servicio, .Horario = 0,
            .CodPostal = codPostal, .PaisIso = "ES", .Origen = origen, .ImporteGasto = coste}
    End Function

    Private Sub AbrirPedido()
        viewModel = New AgenciasViewModel(servicio, configuracion, dialogService, A.Fake(Of IPedidoVentaService), A.Fake(Of IServicioAutenticacion))
        viewModel.ComparadorAgencias = comparador
        viewModel.PestannaNombre = Pestannas.PEDIDOS
        viewModel.cmdCargarDatos.Execute(Nothing)
        viewModel.pedidoSeleccionado = servicio.LeerPedidoParaAgencia("1  ", PEDIDO)
    End Sub

    Private Sub ContestaLaPropuesta(propuesta As PropuestaEnvioDto)
        Dim tcs = respuestaPropuesta
        respuestaPropuesta = New TaskCompletionSource(Of PropuestaEnvioDto)()
        tcs.TrySetResult(propuesta)
    End Sub

    Private Sub ContestaElComparador(agenciaId As Integer)
        Dim tcs = respuestaComparador
        respuestaComparador = New TaskCompletionSource(Of OpcionEnvioAgencia)()
        tcs.TrySetResult(New OpcionEnvioAgencia With {.AgenciaId = agenciaId, .Coste = 3.89D})
    End Sub

    <TestMethod()>
    Public Sub RutaPropia_LaApiProponeElRepartoPropio_SaleElRepartoPropioSinTocarNada()
        AbrirPedido()

        ContestaLaPropuesta(Propuesta(REPARTO_PROPIO))

        Assert.AreEqual(REPARTO_PROPIO, viewModel.agenciaSeleccionada.Numero, "Lo que antes cambiaba el mozo a mano")
        Assert.AreEqual(CByte(0), viewModel.servicioActual.ServicioId, "El servicio de la propuesta (la sombra de #595 ya no ve 48)")
        Assert.AreEqual(0D, viewModel.CosteEnvio, "Sin coste de agencia, como la propuesta")
        A.CallTo(Function() comparador.MasEconomica(A(Of String).Ignored, A(Of String).Ignored, A(Of Decimal).Ignored, A(Of Decimal).Ignored, A(Of String).Ignored)) _
            .MustNotHaveHappened()
        A.CallTo(Function() servicio.LeerPropuestaEnvio("1  ", PEDIDO, 0D)).MustHaveHappenedOnceExactly()
        Assert.AreNotEqual(AgenciasViewModel.AVISO_SIN_PROPUESTA_SERVIDOR, viewModel.mensajeError)
    End Sub

    <TestMethod()>
    Public Sub OtraRuta_LaApiProponeCTT_SaleCTTConSuServicio()
        AbrirPedido()

        ContestaLaPropuesta(Propuesta(CTT, servicio:=48, coste:=3.896450D))

        Assert.AreEqual(CTT, viewModel.agenciaSeleccionada.Numero)
        Assert.AreEqual(CByte(48), viewModel.servicioActual.ServicioId)
        Assert.AreEqual(3.896450D, viewModel.CosteEnvio)
    End Sub

    <TestMethod()>
    Public Sub ElUsuarioPuedeCambiarLaAgenciaAMano()
        AbrirPedido()
        ContestaLaPropuesta(Propuesta(REPARTO_PROPIO))

        viewModel.agenciaSeleccionada = viewModel.listaAgencias.Single(Function(a) a.Numero = CTT)

        Assert.AreEqual(CTT, viewModel.agenciaSeleccionada.Numero)
    End Sub

    <TestMethod()>
    Public Sub FallaLaApi_SeEligeComoAntesYSeAvisaEnLaBarra()
        AbrirPedido()
        ' Al cargar la pantalla el doble no tiene más pedidos que el de la prueba: lo que se mira es lo que pasa después.
        Fake.ClearRecordedCalls(dialogService)

        respuestaPropuesta.TrySetException(New Exception("NestoAPI no contesta (500)"))
        ContestaElComparador(CTT)

        Assert.AreEqual(CTT, viewModel.agenciaSeleccionada.Numero, "La más económica del comparador, como antes")
        Assert.AreEqual(AgenciasViewModel.AVISO_SIN_PROPUESTA_SERVIDOR, viewModel.mensajeError)
        A.CallTo(Sub() dialogService.ShowError(A(Of String).Ignored)).MustNotHaveHappened() ' discreto: sin ventanas
    End Sub

    <TestMethod()>
    Public Sub PropuestaQueNoValeParaLaPantalla_SeEligeComoAntes()
        AbrirPedido()

        ContestaLaPropuesta(Propuesta(REPARTO_PROPIO, codPostal:="28110"))
        ContestaElComparador(CTT)

        Assert.AreEqual(CTT, viewModel.agenciaSeleccionada.Numero)
        Assert.AreNotEqual(AgenciasViewModel.AVISO_SIN_PROPUESTA_SERVIDOR, viewModel.mensajeError)
    End Sub

    <TestMethod()>
    Public Sub PropuestaValeParaLaPantalla_SoloEnviosNuevosAlMismoDestino()
        Assert.IsTrue(AgenciasViewModel.PropuestaValeParaLaPantalla(Propuesta(REPARTO_PROPIO, codPostal:="28001 "), "28001", "ES"))
        Assert.IsTrue(AgenciasViewModel.PropuestaValeParaLaPantalla(Propuesta(CTT), CP, "es"))
        Assert.IsFalse(AgenciasViewModel.PropuestaValeParaLaPantalla(Nothing, CP, "ES"), "Sin propuesta (404)")
        Assert.IsFalse(AgenciasViewModel.PropuestaValeParaLaPantalla(Propuesta(0), CP, "ES"), "Ninguna agencia cubre el destino")
        Assert.IsFalse(AgenciasViewModel.PropuestaValeParaLaPantalla(Propuesta(CTT), "28110", "ES"), "CP cambiado a mano en la pantalla")
        Assert.IsFalse(AgenciasViewModel.PropuestaValeParaLaPantalla(Propuesta(CTT), CP, "PT"), "Otro país")
        Assert.IsFalse(AgenciasViewModel.PropuestaValeParaLaPantalla(Propuesta(CTT, origen:=ContratoEnvioDesdePedido.ORIGEN_PENDIENTE_REUTILIZADO), CP, "ES"), "Pendiente: como siempre")
        Assert.IsFalse(AgenciasViewModel.PropuestaValeParaLaPantalla(Propuesta(CTT, origen:="Ampliacion"), CP, "ES"), "Ampliación: como siempre")
    End Sub

    <TestMethod()>
    Public Sub Contrato_RutaYJsonDeLaPropuesta()
        Dim cultura = Threading.Thread.CurrentThread.CurrentCulture
        Try
            Threading.Thread.CurrentThread.CurrentCulture = New Globalization.CultureInfo("es-ES")
            Assert.AreEqual("EnviosAgencias/Propuesta?empresa=1&pedido=929440&peso=3.5", AgenciaService.RutaPropuestaEnvio("1  ", PEDIDO, 3.5D))
        Finally
            Threading.Thread.CurrentThread.CurrentCulture = cultura
        End Try

        ' Lo que devuelve la API (PropuestaEnvioDTO, con PreserveReferencesHandling: $id).
        Dim json As String = "{""$id"":""1"",""Empresa"":""1"",""Pedido"":929440,""Cliente"":""40198"",""Agencia"":14,""AgenciaNombre"":""Reparto propio""," &
            """Servicio"":0,""Horario"":0,""Retorno"":0,""Bultos"":1,""Peso"":3.5,""CodPostal"":""28001"",""PaisIso"":""ES"",""ImporteGasto"":0.0," &
            """Origen"":""Nuevo"",""AvisosDetalle"":[{""$id"":""2"",""Codigo"":""SinPicking"",""Texto"":""Este pedido no tiene ninguna línea con picking."",""Tipo"":""Confirmar""}]}"
        Dim propuesta As PropuestaEnvioDto = AgenciaService.PropuestaEnvioDeJson(json)

        Assert.AreEqual(14, propuesta.Agencia)
        Assert.AreEqual(CShort(0), propuesta.Servicio)
        Assert.AreEqual("28001", propuesta.CodPostal)
        Assert.AreEqual("ES", propuesta.PaisIso)
        Assert.AreEqual("Nuevo", propuesta.Origen)
        Assert.AreEqual(1, propuesta.AvisosDetalle.Count)
        Assert.IsNull(AgenciaService.PropuestaEnvioDeJson(""))
    End Sub
End Class
