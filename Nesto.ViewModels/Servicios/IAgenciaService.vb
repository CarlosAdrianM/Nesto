Imports System.Collections.ObjectModel
Imports System.Data.Objects
Imports System.Threading.Tasks
Imports Nesto.Contratos
Imports Nesto.Models
Imports Nesto.Models.Nesto.Models

Public Interface IAgenciaService
    Function CargarListaPendientes() As IEnumerable(Of EnvioAgenciaWrapper)
    Function GetEnvioById(Id As Integer) As EnviosAgencia
    Function Insertar(envio As EnviosAgencia) As EnviosAgencia
    Sub Modificar(envio As EnviosAgencia)
    Sub Borrar(Id As Integer)
    Function CargarPedido(empresa As String, numeroPedido As Integer?) As CabPedidoVta
    Function CargarListaReembolsos(empresa As String, agencia As Integer) As ObservableCollection(Of EnviosAgencia)
    Function CargarListaRetornos(empresa As String, agencia As Integer, tipoDeRetorno As Integer) As ObservableCollection(Of EnviosAgencia)
    Function CargarListaEnviosTramitados(empresa As String, agencia As Integer, fechaFiltro As Date) As ObservableCollection(Of EnviosAgencia)
    Function CargarListaEnvios(agencia As Integer) As ObservableCollection(Of EnviosAgencia)
    Function CargarListaEnviosTramitadosPorFecha(empresa As String, fechaFiltro As Date) As ObservableCollection(Of EnviosAgencia)
    Function CargarListaEnviosTramitadosPorCliente(empresa As String, clienteFiltro As String) As ObservableCollection(Of EnviosAgencia)
    Function CargarListaEnviosTramitadosPorNombre(empresa As String, nombreFiltro As String) As ObservableCollection(Of EnviosAgencia)
    Function CargarListaAgencias(empresa As String) As ObservableCollection(Of AgenciasTransporte)
    Function CargarListaEnviosPedido(empresa As String, pedido As Integer) As ObservableCollection(Of EnviosAgencia)
    Function CargarAgencia(agencia As Integer) As AgenciasTransporte
    Function CargarListaHistoriaEnvio(envio As Integer) As ObservableCollection(Of EnviosHistoria)
    Function CargarPedidoPorNumero(pedido As Integer) As CabPedidoVta
    Function CargarPedidoPorNumero(pedido As Integer, espejo As Boolean) As CabPedidoVta
    Function CargarPedidoPorFactura(numeroPedido As String) As CabPedidoVta
    Function CargarClientePorUnDato(empresa As String, datoABuscar As String) As Clientes
    Function CargarMultiusuario(empresa As String, multiusuario As Integer) As MultiUsuarios
    Function CalcularSumaContabilidad(empresa As String, cuentaReembolsos As String) As Double?
    Function CargarListaEmpresas() As ObservableCollection(Of Empresas)
    Function CargarClientePrincipal(empresa As String, cliente As String) As Clientes
    Function CargarLineasPedidoPendientes(pedido As Integer) As List(Of LinPedidoVta)
    Function CargarLineasPedidoSinPicking(pedido As Integer) As List(Of LinPedidoVta)
    Function HayAlgunaLineaConPicking(empresa As String, pedido As Integer) As Boolean
    Function CargarAgenciaPorNombreYCuentaReembolsos(empresa As String, cuentaReembolsos As String, nombreAgencia As String) As AgenciasTransporte
    Function CargarEnvio(empresa As String, pedido As Integer) As EnviosAgencia
    Function CargarExtractoCliente(empresa As String, cliente As String, positivos As Boolean) As ObservableCollection(Of ExtractoCliente)
    Function CargarPagoExtractoClientePorEnvio(envio As EnviosAgencia, concepto As String, importeAnterior As Double) As ObservableCollection(Of ExtractoCliente)
    Function CargarAgenciaPorRuta(empresa As String, ruta As String) As AgenciasTransporte
    Function CargarCliente(empresa As String, cliente As String, contacto As String) As Clientes
    Function CargarEnvioPorClienteYDireccion(cliente As String, contacto As String, direccion As String) As EnviosAgencia
    Function CargarDeudasCliente(cliente As String, fechaReclamar As Date) As List(Of ExtractoCliente)
    Function TramitarEnvio(envio As EnviosAgencia) As String
    Function ContabilizarReembolso(envio As EnviosAgencia) As Integer
    Function CalcularMovimientoLiq(env As EnviosAgencia) As ExtractoCliente
    Function CalcularMovimientoLiq(env As EnviosAgencia, reembolsoAnterior As Double) As ExtractoCliente
    Function GenerarConcepto(envio As EnviosAgencia) As String
    Function EnviarCorreoEntregaAgencia(envioActual As EnvioAgenciaWrapper) As Task
    Function EsTodoElPedidoOnline(empresa As String, pedido As Integer) As Boolean
    Function GuardarLlamadaAgencia(respuesta As RespuestaAgencia) As Task
End Interface
