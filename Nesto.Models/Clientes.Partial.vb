Namespace Nesto.Models

    ''' <summary>Lo que trae la respuesta de POST api/Clientes además de las columnas del cliente.</summary>
    Partial Public Class Clientes
        ''' <summary>NestoAPI#876: en el alta con origen CAMBIO-FISCAL, el número de cliente anterior que se ha guardado (Nothing si no lo hay).</summary>
        Public Property ClienteAnterior As String
        ''' <summary>NestoAPI#876: aviso del alta sobre el cliente anterior (p. ej. tiene el mismo NIF). Nothing si no lo hay.</summary>
        Public Property AvisoClienteAnterior As String
    End Class

End Namespace
