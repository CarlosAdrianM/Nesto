Imports System.Collections.ObjectModel
Imports Nesto.Modulos.Rapports.RapportsModel.SeguimientoClienteDTO

Public Interface IRapportService
    Function cargarListaRapports(empresa As String, cliente As String, contacto As String) As Task(Of ObservableCollection(Of SeguimientoClienteDTO))
    Function cargarListaRapports(vendedor As String, fecha As Date) As Task(Of ObservableCollection(Of SeguimientoClienteDTO))
    Function crearRapport(rapport As SeguimientoClienteDTO) As Task(Of String)
    Function CrearCita(rapport As SeguimientoClienteDTO, fechaAviso As Date) As Task(Of String)
    Function CrearTareaPlanner(rapport As SeguimientoClienteDTO) As Task(Of String)
    Function cargarListaRapportsFiltrada(vendedor As String, filtro As String) As Task(Of ObservableCollection(Of SeguimientoClienteDTO))
    Function CargarListaEstados() As List(Of idShortDescripcion)
    Function CargarListaTipos() As List(Of idDescripcion)
    ' Nesto#558 (NestoAPI#716): motivo y comentario opcionales del cambio de estado (PUT Clientes/DejarDeVisitar); con
    ' Nothing la API anota «DejarDeVisitar» ella sola.
    Function QuitarDeMiListado(rapport As SeguimientoClienteDTO, vendedorEstetica As String, vendedorPeluqueria As String,
                               Optional motivoCambioEstado As String = Nothing, Optional comentarioCambioEstado As String = Nothing) As Task(Of Boolean)
    Function CargarClientesProbabilidad(vendedor As String, tipoInteraccion As String, grupoSubgrupo As String) As Task(Of List(Of ClienteProbabilidadVenta))
    ' NestoAPI#603: lista por prioridad y cadencia, con el ritmo del vendedor. Si la API publicada aún no
    ' tiene el endpoint (404), cae a CargarClientesProbabilidad y devuelve Ritmo = Nothing.
    Function CargarSugerenciasContacto(vendedor As String, tipoInteraccion As String, grupoSubgrupo As String) As Task(Of SugerenciasContactoRespuesta)
    ' Nesto#521: GET api/Vendedores/VisiblesEnSugerencias. Los vendedores cuyas sugerencias puede ver el usuario (el suyo,
    ' su equipo si es jefe de ventas, todos si es Dirección). Lista vacía si falla o la API publicada no lo tiene.
    Function CargarVendedoresSugerencias() As Task(Of List(Of VendedorSugerencias))
    Function CargarResumenRapports(empresa As String, cliente As String, contacto As String) As Task(Of String)
    Function CargarResumenVentasCliente(clienteId As String, modoComparativa As String, agruparPor As String) As Task(Of ResumenVentasClienteResponse)
    Function CargarDetalleVentasProducto(clienteId As String, filtro As String, modoComparativa As String, agruparPor As String) As Task(Of ResumenVentasClienteResponse)
    ' NestoAPI#593: estadística del cheque regalo. Resumen: Nothing si no hay campaña o la API no lo tiene.
    Function CargarResumenChequesRegalo() As Task(Of ResumenChequesRegalo)
    Function CargarChequesRegaloPendientes(vendedor As String) As Task(Of List(Of ChequeRegaloPendiente))
    Function DescargarFacturaPdf(empresa As String, numeroFactura As String) As Task(Of Byte())
    Function CopiarSeguimientos(empresa As String, clienteOrigen As String, contactoOrigen As String, clienteDestino As String, contactoDestino As String, eliminarOrigen As Boolean) As Task(Of Integer)
End Interface
