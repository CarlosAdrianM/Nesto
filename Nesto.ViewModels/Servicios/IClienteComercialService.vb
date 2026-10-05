Imports Nesto.Infrastructure.Models
Imports Nesto.Models
Imports Nesto.Modulos.PedidoVenta

Public Interface IClienteComercialService
    Function ModificarExtractoCliente(extracto As ExtractoClienteDTO) As Task
    ''' <summary>
    ''' Nesto#340 (1C.8, último resto EF del VM): empresas para el combo de la cabecera
    ''' (GET Empresas). Sustituye a DbContext.Empresas en el VM.
    ''' </summary>
    Function LeerEmpresas() As Task(Of List(Of EmpresaModel))
    ''' <summary>
    ''' Nesto#340 (1C.8, slice 4): ficha completa del cliente (GET Clientes), incluidos
    ''' VendedoresGrupoProducto y PersonasContacto. Sustituye a DbContext.Clientes en el VM.
    ''' </summary>
    Function LeerCliente(empresa As String, cliente As String, contacto As String) As Task(Of ClienteJson)
    ''' <summary>
    ''' Nesto#340 (1C.8, slice 5): CCCs del cliente/contacto (GET Clientes/CCCs) como POCOs
    ''' con dirty flag. Sustituye a DbContext.CCC en el VM.
    ''' </summary>
    Function LeerCCCs(empresa As String, cliente As String, contacto As String) As Task(Of List(Of CCCModel))
    ''' <summary>
    ''' Nesto#340 (1C.8, slice 5): catálogo de estados de CCC (GET Clientes/EstadosCCC).
    ''' Sustituye a DbContext.EstadosCCC en el VM.
    ''' </summary>
    Function LeerEstadosCCC(empresa As String) As Task(Of List(Of EstadoCCCModel))
    ''' <summary>
    ''' Nesto#340 (1C.8, slice 5): guarda los CCC modificados (PUT Clientes/CCCs, upsert en el
    ''' servidor) y devuelve los avisos de efectos/pedidos que apuntan a otro CCC.
    ''' Sustituye a DbContext.SaveChanges en el VM.
    ''' </summary>
    Function GuardarCCCs(peticion As GuardarCCCsRequest) As Task(Of GuardarCCCsRespuesta)
    ''' <summary>
    ''' Nesto#458: el equipo de ventas de un vendedor a fecha de hoy (GET Vendedores?empresa=X
    ''' &amp;vendedor=Y; incluye al propio vendedor). Para filtrar el combo de la ventana; la
    ''' garantía de permisos vive en el servidor.
    ''' </summary>
    Function LeerVendedoresEquipo(empresa As String, vendedor As String) As Task(Of List(Of VendedorDTO))
    ''' <summary>
    ''' Nesto#259: el correo de facturas del cliente de esa factura (GET Facturas/CorreoFacturas), para proponerlo
    ''' al mandarla. Cadena vacía si no tiene.
    ''' </summary>
    Function LeerCorreoFacturas(empresa As String, numeroFactura As String) As Task(Of String)
    ''' <summary>
    ''' Nesto#259: manda las facturas en un solo correo a los correos escritos («;» o «,» para varios)
    ''' (POST Facturas/EnviarPorCorreo). Si la API lo rechaza (400), lanza la excepción con su motivo; si el correo
    ''' no sale (502), devuelve el resultado con Enviado = False y el mensaje.
    ''' </summary>
    Function EnviarFacturasPorCorreo(empresa As String, facturas As List(Of String), correos As String) As Task(Of ResultadoEnvioFacturasCorreo)
End Interface
