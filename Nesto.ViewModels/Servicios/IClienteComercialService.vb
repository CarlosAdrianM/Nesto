Imports Nesto.Modulos.PedidoVenta

Public Interface IClienteComercialService
    Function ModificarExtractoCliente(extracto As ExtractoClienteDTO) As Task
    ''' <summary>
    ''' Nesto#340 (1C.8, slice 4): ficha completa del cliente (GET Clientes), incluidos
    ''' VendedoresGrupoProducto y PersonasContacto. Sustituye a DbContext.Clientes en el VM.
    ''' </summary>
    Function LeerCliente(empresa As String, cliente As String, contacto As String) As Task(Of ClienteJson)
End Interface
