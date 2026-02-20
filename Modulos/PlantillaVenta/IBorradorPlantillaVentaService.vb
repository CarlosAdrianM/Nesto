''' <summary>
''' Servicio para gestionar borradores de PlantillaVenta guardados localmente.
''' Issue #286: Borradores de PlantillaVenta
''' </summary>
Public Interface IBorradorPlantillaVentaService
    ''' <summary>
    ''' Guarda un borrador en el almacenamiento local.
    ''' </summary>
    ''' <param name="borrador">El borrador a guardar (sin Id, se generará automáticamente)</param>
    ''' <returns>El borrador guardado con su Id asignado</returns>
    Function GuardarBorrador(borrador As BorradorPlantillaVenta) As BorradorPlantillaVenta

    ''' <summary>
    ''' Obtiene la lista de todos los borradores guardados (sin las líneas, solo metadatos).
    ''' </summary>
    ''' <returns>Lista de borradores ordenados por fecha (más reciente primero)</returns>
    Function ObtenerBorradores() As List(Of BorradorPlantillaVenta)

    ''' <summary>
    ''' Carga un borrador específico por su ID (con todas las líneas).
    ''' </summary>
    ''' <param name="id">ID del borrador</param>
    ''' <returns>El borrador completo, o Nothing si no existe</returns>
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

    ''' <summary>
    ''' Crea un borrador a partir de un JSON (compatible con camelCase de NestoApp).
    ''' Issue #288: Crear borrador desde JSON del portapapeles
    ''' </summary>
    ''' <param name="json">JSON del borrador (camelCase o PascalCase)</param>
    ''' <returns>El borrador creado y guardado</returns>
    ''' <exception cref="ArgumentException">Si el JSON es inválido o no contiene datos mínimos</exception>
    Function CrearBorradorDesdeJson(json As String) As BorradorPlantillaVenta

    ''' <summary>
    ''' Comprueba si un texto es un JSON válido que puede deserializarse como borrador.
    ''' Requiere al menos un cliente y alguna línea de producto o regalo.
    ''' Issue #288: Crear borrador desde JSON del portapapeles
    ''' </summary>
    ''' <param name="json">Texto a validar</param>
    ''' <returns>True si es un JSON válido de borrador</returns>
    Function EsJsonBorradorValido(json As String) As Boolean
End Interface
