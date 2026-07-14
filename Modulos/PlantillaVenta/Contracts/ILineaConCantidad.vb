''' <summary>
''' Interfaz para líneas que tienen cantidad editable.
''' Permite que SelectorLineasPlantillaVenta trabaje con LineaPlantillaVenta y LineaRegalo.
''' Issue #94: Sistema Ganavisiones - FASE 7
''' </summary>
Public Interface ILineaConCantidad
    Property cantidad As Integer
    Property cantidadOferta As Integer
    Property aplicarDescuentoFicha As Boolean?
    ''' <summary>
    ''' Nesto#401: habilita el control de cantidad oferta del selector. Con oferta ya puesta
    ''' siempre editable (para poder reducirla o quitarla); sin oferta, según la ficha.
    ''' </summary>
    ReadOnly Property puedeEditarCantidadOferta As Boolean
End Interface
