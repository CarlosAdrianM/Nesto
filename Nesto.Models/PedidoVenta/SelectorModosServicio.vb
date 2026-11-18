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

    ''' <summary>Sugerencia 1340: el 6 se ofrece si viene en los permitidos o en la lista de modos de la API.</summary>
    Protected Overrides Function Ofrece(sugerencia As ISugerenciaModos, modo As Byte) As Boolean
        Dim servicio = TryCast(sugerencia, ModoServicioSugeridoDTO)
        If servicio IsNot Nothing Then
            Return servicio.Ofrece(modo)
        End If
        Return MyBase.Ofrece(sugerencia, modo)
    End Function

    ''' <summary>
    ''' Sugerencia 1340: el texto que va debajo del combo con este modo elegido («Saldrá junto con la nota de entrega
    ''' 929701.»), tal como lo redacta la API. Nothing si no hay o la API no ha contestado.
    ''' </summary>
    Public Function Explicacion(modo As Byte) As String
        Return TryCast(UltimaSugerencia, ModoServicioSugeridoDTO)?.ExplicacionDe(modo)
    End Function

    ''' <summary>El aviso que se enseña al pasar de un modo que ya no tiene sentido al que sí.</summary>
    Public Overloads Shared Function TextoAvisoCambio(anterior As Byte, nuevo As Byte, motivo As String) As String
        Return TextoAvisoCambio(ModosServicio.Lista, anterior, nuevo, motivo)
    End Function
End Class
