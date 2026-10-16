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
''' Recoger producto (16/10/26, pedidos 928500 y 928520, envíos 249764 y 249772): la plantilla crea la
''' etiqueta pendiente de CTT con Retorno = 1 y, al imprimirla en la pestaña Pedidos, se guardaba con
''' Retorno = 0 y CTT no recogía nada. Al abrir el pedido se cargaba bien el retorno del pendiente, pero
''' al teclear el peso (obligatorio; la pendiente nace con peso 0) el setter de Peso vuelve a elegir la
''' agencia y el setter de la agencia ponía SIEMPRE los defectos (retorno «sin retorno», servicio y
''' horario por defecto), aunque la agencia fuera la misma. Al imprimir se guarda lo de la pantalla.
''' </summary>
<TestClass()>
Public Class AgenciasViewModelRetornoPendienteTests
    Private Const ASM As Integer = 1
    Private Const CTT As Integer = 13
    Private Const PEDIDO_CON_PENDIENTE As Integer = 928520
    Private Const PEDIDO_SIN_PENDIENTE As Integer = 928521

    Private servicio As IAgenciaService
    Private configuracion As IConfiguracion
    Private dialogService As IServicioDialogos
    Private comparador As IServicioComparadorAgencias
    Private respuestaComparador As TaskCompletionSource(Of OpcionEnvioAgencia)
    Private agencias As Func(Of ObservableCollection(Of AgenciasTransporte))
    Private viewModel As AgenciasViewModel

    <TestInitialize()>
    Public Sub Initialize()
        servicio = A.Fake(Of IAgenciaService)
        configuracion = A.Fake(Of IConfiguracion)
        dialogService = A.Fake(Of IServicioDialogos)
        comparador = A.Fake(Of IServicioComparadorAgencias)
        ' El comparador del servidor contesta TARDE, como en la vida real (ContestaElComparador).
        respuestaComparador = New TaskCompletionSource(Of OpcionEnvioAgencia)()
        A.CallTo(Function() comparador.MasEconomica(A(Of String).Ignored, A(Of String).Ignored, A(Of Decimal).Ignored, A(Of Decimal).Ignored, A(Of String).Ignored)) _
            .ReturnsLazily(Function() respuestaComparador.Task)

        A.CallTo(Function() configuracion.leerParametro("1", "EmpresaPorDefecto")).Returns("1  ")
        A.CallTo(Function() configuracion.leerParametro(A(Of String).Ignored, "UltNumPedidoVta")).Returns("1")

        Dim empresa = A.Fake(Of Empresas)
        empresa.Número = "1  "
        A.CallTo(Function() servicio.CargarListaEmpresas()).Returns(New ObservableCollection(Of Empresas) From {empresa})
        agencias = Function() New ObservableCollection(Of AgenciasTransporte) From {Agencia(ASM, "ASM"), Agencia(CTT, "CTT")}
        A.CallTo(Function() servicio.CargarListaAgencias(A(Of String).Ignored)).ReturnsLazily(Function() agencias())

        ConfigurarPedido(PEDIDO_CON_PENDIENTE)
        ConfigurarPedido(PEDIDO_SIN_PENDIENTE)
        A.CallTo(Function() servicio.CargarEnvio(A(Of String).Ignored, PEDIDO_SIN_PENDIENTE)).Returns(Nothing)
        ' La pendiente que crea la plantilla con «Recoger producto»: CTT, retorno 1, peso 0.
        A.CallTo(Function() servicio.CargarEnvio(A(Of String).Ignored, PEDIDO_CON_PENDIENTE)).
            ReturnsLazily(Function() New EnviosAgencia With {.Numero = 249772, .Empresa = "1  ", .Agencia = CTT, .Pedido = PEDIDO_CON_PENDIENTE,
                .Estado = -1, .Servicio = 24, .Horario = 0, .Retorno = 1, .Peso = 0D, .CodPostal = "28001",
                .AgenciasTransporte = Agencia(CTT, "CTT")})
    End Sub

    Private Shared Function Agencia(numero As Integer, nombre As String) As AgenciasTransporte
        Return New AgenciasTransporte With {.Empresa = "1  ", .Numero = numero, .Nombre = nombre}
    End Function

    Private Sub ContestaElComparador(agenciaId As Integer)
        Dim tcs = respuestaComparador
        respuestaComparador = New TaskCompletionSource(Of OpcionEnvioAgencia)()
        tcs.TrySetResult(New OpcionEnvioAgencia With {.AgenciaId = agenciaId, .Coste = 4.5D})
    End Sub

    Private Sub ConfigurarPedido(numero As Integer)
        Dim pedido = New PedidoAgenciaModel With {
            .Empresa = "1  ",
            .Número = numero,
            .Nº_Cliente = "40198",
            .Contacto = "0",
            .Vendedor = "MAN",
            .Clientes = New ClienteAgenciaModel With {.Nombre = "CLIENTE", .Dirección = "CALLE", .Población = "MADRID", .Provincia = "MADRID", .CodPostal = "28001", .Teléfono = "911234567"}
        }
        A.CallTo(Function() servicio.LeerPedidoParaAgencia(A(Of String).Ignored, numero)).Returns(pedido)
        A.CallTo(Function() servicio.LeerPedidoParaAgenciaPorNumero(numero, A(Of Boolean).Ignored)).Returns(pedido)
        A.CallTo(Function() servicio.CargarListaEnviosPedido(A(Of String).Ignored, numero)).Returns(New ObservableCollection(Of EnviosAgencia))
    End Sub

    Private Sub AbrirPedido(numero As Integer)
        If viewModel Is Nothing Then
            viewModel = New AgenciasViewModel(servicio, configuracion, dialogService,
                                              A.Fake(Of IPedidoVentaService), A.Fake(Of IServicioAutenticacion))
            viewModel.ComparadorAgencias = comparador
            viewModel.PestannaNombre = Pestannas.PEDIDOS
            viewModel.cmdCargarDatos.Execute(Nothing)
        End If
        viewModel.pedidoSeleccionado = servicio.LeerPedidoParaAgencia("1  ", numero)
    End Sub

    ''' <summary>Lo que hace InsertarRegistro con la pendiente al imprimir (la agencia del envío es CTT).</summary>
    Private Function RetornoYServicioQueSeGuardanAlImprimir() As (Retorno As Byte, Servicio As Byte)
        Dim pendiente = servicio.CargarEnvio("1  ", PEDIDO_CON_PENDIENTE)
        viewModel.AplicarServicioYRetornoAlPendiente(pendiente, New AgenciaCTT(), CTT)
        Return (pendiente.Retorno, pendiente.Servicio)
    End Function

    <TestMethod()>
    Public Sub RecogerProducto_AlTeclearElPesoDeUnaPendienteDeCTT_SeGuardaConRetorno()
        AbrirPedido(PEDIDO_CON_PENDIENTE)
        Assert.AreEqual(CTT, viewModel.agenciaSeleccionada.Numero, "Precondición: la pantalla en la agencia de la pendiente")
        Assert.AreEqual(CByte(1), viewModel.retornoActual.id, "Precondición: al abrir el pedido se enseña el retorno de la pendiente")

        ' Andre teclea el peso (7 kg): el setter vuelve a elegir agencia y el comparador del servidor dice CTT.
        viewModel.Peso = 7D
        ContestaElComparador(CTT)

        Assert.AreEqual(CTT, viewModel.agenciaSeleccionada.Numero)
        Assert.AreEqual(CByte(1), viewModel.retornoActual.id, "El peso no puede quitar el retorno de la pendiente")
        Assert.AreEqual(CByte(24), viewModel.servicioActual.ServicioId, "Ni el servicio")
        Dim guardado = RetornoYServicioQueSeGuardanAlImprimir()
        Assert.AreEqual(CByte(1), guardado.Retorno, "Al imprimir, la pendiente tiene que seguir con retorno")
        Assert.AreEqual(CByte(24), guardado.Servicio)
    End Sub

    <TestMethod()>
    Public Sub RecogerProducto_SiLaPantallaPasaPorOtraAgenciaYVuelveACTT_SeRecuperaElRetornoDeLaPendiente()
        ' El comparador (local y luego el del servidor) puede llevar la pantalla a otra agencia y devolverla.
        AbrirPedido(PEDIDO_CON_PENDIENTE)

        viewModel.agenciaSeleccionada = viewModel.listaAgencias.Single(Function(a) a.Numero = ASM)
        viewModel.agenciaSeleccionada = viewModel.listaAgencias.Single(Function(a) a.Numero = CTT)

        Assert.AreEqual(CByte(1), viewModel.retornoActual.id)
        Assert.AreEqual(CByte(24), viewModel.servicioActual.ServicioId)
        Assert.AreEqual(CByte(1), RetornoYServicioQueSeGuardanAlImprimir().Retorno)
    End Sub

    <TestMethod()>
    Public Sub SinPendiente_ElRetornoElegidoAManoNoLoPisaElPeso()
        agencias = Function() New ObservableCollection(Of AgenciasTransporte) From {Agencia(CTT, "CTT")}
        AbrirPedido(PEDIDO_SIN_PENDIENTE)
        Assert.AreEqual(CTT, viewModel.agenciaSeleccionada.Numero, "Precondición")
        viewModel.retornoActual = viewModel.listaTiposRetorno.Single(Function(r) r.id = 1)

        viewModel.Peso = 3D
        ContestaElComparador(CTT)

        Assert.AreEqual(CByte(1), viewModel.retornoActual.id)
    End Sub

    <TestMethod()>
    Public Sub AlCambiarDePedido_ConLaMismaAgencia_ElRetornoVuelveAlDefecto()
        ' Lo elegido para un pedido no se arrastra al siguiente.
        agencias = Function() New ObservableCollection(Of AgenciasTransporte) From {Agencia(CTT, "CTT")}
        AbrirPedido(PEDIDO_CON_PENDIENTE)
        Assert.AreEqual(CByte(1), viewModel.retornoActual.id, "Precondición")

        AbrirPedido(PEDIDO_SIN_PENDIENTE)

        Assert.AreEqual(CTT, viewModel.agenciaSeleccionada.Numero)
        Assert.AreEqual(CByte(0), viewModel.retornoActual.id)
        Assert.AreEqual(CByte(48), viewModel.servicioActual.ServicioId)
    End Sub

End Class
