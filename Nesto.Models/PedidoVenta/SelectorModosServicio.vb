''' <summary>
''' Nesto#484 / NestoAPI#518: lo común a los dos combos «Servir» (plantilla y detalle de pedido). Desde Nesto#493
''' el núcleo vive en <see cref="SelectorModos"/> (compartido con «Facturación»); aquí solo queda la lista de
''' modos de servicio y las firmas Shared que ya usaban los ViewModels y los tests.
''' </summary>
Public Class SelectorModosServicio
    Inherits SelectorModos

    Public Sub New()
        MyBase.New(ModosServicio.Lista)
    End Sub

    ''' <summary>El aviso que se enseña al pasar de un modo que ya no tiene sentido al que sí.</summary>
    Public Overloads Shared Function TextoAvisoCambio(anterior As Byte, nuevo As Byte, motivo As String) As String
        Return TextoAvisoCambio(ModosServicio.Lista, anterior, nuevo, motivo)
    End Function
End Class
