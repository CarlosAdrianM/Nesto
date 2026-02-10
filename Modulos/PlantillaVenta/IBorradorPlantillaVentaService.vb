Imports Nesto.Models

''' <summary>
''' Servicio para gestionar borradores de PlantillaVenta guardados localmente.
''' Issue #286: Borradores de PlantillaVenta
''' </summary>
Public Interface IBorradorPlantillaVentaService
    ''' <summary>
    ''' Guarda un borrador en el almacenamiento local.
    ''' </summary>
    ''' <param name="pedido">El pedido a guardar</param>
    ''' <param name="nombreCliente">Nombre del cliente (para mostrar en la lista)</param>
    ''' <param name="mensajeError">Mensaje de error que causó la creación del borrador (opcional)</param>
    ''' <returns>El borrador creado con su ID asignado</returns>
    Function GuardarBorrador(pedido As PedidoVentaDTO, nombreCliente As String, Optional mensajeError As String = Nothing) As BorradorPlantillaVenta

    ''' <summary>
    ''' Obtiene la lista de todos los borradores guardados.
    ''' </summary>
    ''' <returns>Lista de borradores ordenados por fecha (más reciente primero)</returns>
    Function ObtenerBorradores() As List(Of BorradorPlantillaVenta)

    ''' <summary>
    ''' Carga un borrador específico por su ID.
    ''' </summary>
    ''' <param name="id">ID del borrador</param>
    ''' <returns>El borrador con el pedido completo, o Nothing si no existe</returns>
    Function CargarBorrador(id As String) As BorradorPlantillaVenta

    ''' <summary>
    ''' Elimina un borrador del almacenamiento local.
    ''' </summary>
    ''' <param name="id">ID del borrador a eliminar</param>
    ''' <returns>True si se eliminó correctamente</returns>
    Function EliminarBorrador(id As String) As Boolean

    ''' <summary>
    ''' Elimina todos los borradores del almacenamiento local.
    ''' </summary>
    ''' <returns>Número de borradores eliminados</returns>
    Function EliminarTodosBorradores() As Integer

    ''' <summary>
    ''' Obtiene el número de borradores guardados.
    ''' </summary>
    ''' <returns>Número de borradores</returns>
    Function ContarBorradores() As Integer
End Interface
