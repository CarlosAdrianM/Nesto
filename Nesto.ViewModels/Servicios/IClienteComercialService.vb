Imports Nesto.Modulos.PedidoVenta

Public Interface IClienteComercialService
    Function ModificarExtractoCliente(extracto As ExtractoClienteDTO) As Task
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
End Interface
