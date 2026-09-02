Imports System.Collections.ObjectModel
Imports Nesto.Models
Imports Nesto.Models.Nesto.Models

Public Interface IAgenciaService
    Function CargarListaPendientes() As IEnumerable(Of EnvioAgenciaWrapper)
    ' Nesto#340 (A2): GetEnvioById eliminado — no lo llamaba nadie.
    Function Insertar(envio As EnviosAgencia) As EnviosAgencia
    Sub Modificar(envio As EnviosAgencia)
    Sub Borrar(Id As Integer)
    ' Nesto#340 (A3): el pedido de Agencias ya no es la entidad EF CabPedidoVta, sino un POCO
    ' que viene de GET api/PedidosVenta/ParaAgencia. Los cuatro modos cubren los cuatro caminos
    ' que antes iban por EF (por empresa+numero, por numero, por factura y por texto de cliente).
    Function LeerPedidoParaAgencia(empresa As String, numeroPedido As Integer?) As PedidoAgenciaModel
    Function CargarListaReembolsos(empresa As String, agencia As Integer) As ObservableCollection(Of EnviosAgencia)
    Function CargarListaRetornos(empresa As String, agencia As Integer, tipoDeRetorno As Integer) As ObservableCollection(Of EnviosAgencia)
    Function CargarListaEnviosTramitados(empresa As String, agencia As Integer, fechaFiltro As Date) As ObservableCollection(Of EnviosAgencia)
    Function CargarListaEnvios(agencia As Integer) As ObservableCollection(Of EnviosAgencia)
    Function CargarListaEnviosTramitadosPorFecha(empresa As String, fechaFiltro As Date) As ObservableCollection(Of EnviosAgencia)
    Function CargarListaIncidentados(empresa As String) As ObservableCollection(Of EnviosAgencia)
    Function CargarListaEnviosTramitadosPorCliente(empresa As String, clienteFiltro As String) As ObservableCollection(Of EnviosAgencia)
    Function CargarListaEnviosTramitadosPorNombre(empresa As String, nombreFiltro As String) As ObservableCollection(Of EnviosAgencia)
    Function CargarListaAgencias(empresa As String) As ObservableCollection(Of AgenciasTransporte)
    Function CargarListaEnviosPedido(empresa As String, pedido As Integer) As ObservableCollection(Of EnviosAgencia)
    Function CargarAgencia(agencia As Integer) As AgenciasTransporte
    Function CargarListaHistoriaEnvio(envio As Integer) As ObservableCollection(Of EnviosHistoria)
    Function LeerPedidoParaAgenciaPorNumero(numeroPedido As Integer, incluirEspejo As Boolean) As PedidoAgenciaModel
    Function LeerPedidoParaAgenciaPorFactura(numeroFactura As String) As PedidoAgenciaModel
    ' Sustituye a CargarClientePorUnDato + navegar cliente.CabPedidoVta (que reventaba con
    ' ObjectDisposedException porque el contexto ya estaba cerrado). Ahora es una sola llamada.
    Function LeerPedidoParaAgenciaPorTextoCliente(empresa As String, texto As String) As PedidoAgenciaModel
    Function CalcularSumaContabilidad(empresa As String, cuentaReembolsos As String) As Double?
    Function CargarListaEmpresas() As ObservableCollection(Of Empresas)
    ''' <summary>Nesto#340 (slice A3): Agencias solo comprueba que el cliente EXISTE y esta de
    ''' alta, asi que se pregunta eso y no se trae la ficha entera.</summary>
    Function ExisteClientePrincipalActivo(empresa As String, cliente As String) As Boolean
    Function HayAlgunaLineaConPicking(empresa As String, pedido As Integer) As Boolean
    Function CargarAgenciaPorNombreYCuentaReembolsos(empresa As String, cuentaReembolsos As String, nombreAgencia As String) As AgenciasTransporte
    Function CargarEnvio(empresa As String, pedido As Integer) As EnviosAgencia
    Function CargarExtractoCliente(empresa As String, cliente As String, positivos As Boolean) As ObservableCollection(Of ExtractoCliente)
    Function CargarPagoExtractoClientePorEnvio(envio As EnviosAgencia, concepto As String, importeAnterior As Double) As ObservableCollection(Of ExtractoCliente)
    Function CargarAgenciaPorRuta(empresa As String, ruta As String) As AgenciasTransporte
    Function CargarEnvioPorClienteYDireccion(cliente As String, contacto As String, direccion As String) As EnviosAgencia
    Function CargarDeudasCliente(cliente As String, fechaReclamar As Date) As List(Of ExtractoCliente)
    Function TramitarEnvio(envio As EnviosAgencia) As String
    ' Innovatrans (registrar al imprimir): tramita el envío contra la agencia en el servidor
    ' (POST api/EnviosAgencias/{id}/Tramitar) y devuelve el albarán + bultos + etiqueta ZPL.
    Function TramitarEnvioRemoto(numeroEnvio As Integer) As Task(Of TramitarEnvioResultadoDto)
    ' Nesto#411 (NestoAPI#316): anula en la agencia un envío YA registrado; el servidor lo devuelve
    ' a etiqueta pendiente (Estado -1, sin albarán). Lanza con el motivo de la agencia si rechaza
    ' (p. ej. la ventana de edición del día ya cerró).
    Function AnularEnvioRemoto(numeroEnvio As Integer) As Task
    ' Nesto#411 (NestoAPI#317): modifica en la agencia un envío YA registrado (dirección/CP...) y
    ' devuelve la etiqueta ZPL reimpresa (la etiqueta lleva CP/población impresos).
    Function ModificarEnvioRemoto(numeroEnvio As Integer, datos As ModificarEnvioAgenciaDto) As Task(Of TramitarEnvioResultadoDto)
    ' Actualiza el estado de un envío a demanda (sin esperar al job de Hangfire de cada 2h).
    Function ActualizarSeguimientoEnvio(numeroEnvio As Integer) As Task(Of SeguimientoActualizadoDto)
    Function ContabilizarReembolso(envio As EnviosAgencia) As Integer
    Function CalcularMovimientoLiq(env As EnviosAgencia) As ExtractoCliente
    Function CalcularMovimientoLiq(env As EnviosAgencia, reembolsoAnterior As Double) As ExtractoCliente
    Function GenerarConcepto(envio As EnviosAgencia) As String
    Function EnviarCorreoEntregaAgencia(envioActual As EnvioAgenciaWrapper) As Task
    Function EsTodoElPedidoOnline(empresa As String, pedido As Integer) As Boolean
    Function GuardarLlamadaAgencia(respuesta As RespuestaAgencia) As Task
    Function ImporteReembolso(empresa As String, pedido As Integer) As Task(Of Decimal)

    ' Nesto#359: envía un correo (vía /api/Correos/Enviar) con el PDF de la factura del
    ' pedido adjunto. Lo usa AgenciaCanteras.LlamadaWebService para notificar recogidas a
    ' Canteras (que necesitan la factura para el DUA), pero es genérico: cualquier flujo
    ' puede llamarlo. Si el pedido aún no tiene factura, devuelve Exito=False.
    Function EnviarCorreoConFacturaDelPedido(empresa As String, numeroPedido As Integer, destinatario As String, asunto As String, cuerpo As String) As Task(Of (Exito As Boolean, Mensaje As String))
End Interface
