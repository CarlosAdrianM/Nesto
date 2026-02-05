''' <summary>
''' Interfaz para líneas que tienen cantidad editable.
''' Permite que SelectorLineasPlantillaVenta trabaje con LineaPlantillaVenta y LineaRegalo.
''' Issue #94: Sistema Ganavisiones - FASE 7
''' </summary>
Public Interface ILineaConCantidad
    Property cantidad As Integer
    Property cantidadOferta As Integer
    Property aplicarDescuentoFicha As Boolean?
End Interface
