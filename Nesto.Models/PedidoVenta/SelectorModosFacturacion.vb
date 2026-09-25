''' <summary>
''' Nesto#493 / NestoAPI#542: el combo «Facturación» (plantilla y detalle de pedido). El núcleo es
''' <see cref="SelectorModos"/>, el mismo que el de «Servir»; aquí solo la lista de modos de facturación.
''' </summary>
Public Class SelectorModosFacturacion
    Inherits SelectorModos

    Public Sub New()
        MyBase.New(ModosFacturacion.Lista)
    End Sub

    ''' <summary>El aviso que se enseña al pasar de un modo que ya no se puede elegir al que sí.</summary>
    Public Overloads Shared Function TextoAvisoCambio(anterior As Byte, nuevo As Byte, motivo As String) As String
        Return TextoAvisoCambio(ModosFacturacion.Lista, anterior, nuevo, motivo)
    End Function
End Class
