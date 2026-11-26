Namespace Nesto.Models

    ''' <summary>Lo que trae la respuesta de POST api/Clientes además de las columnas del cliente.</summary>
    Partial Public Class Clientes
        ''' <summary>NestoAPI#876: en el alta con origen CAMBIO-FISCAL, el número de cliente anterior que se ha guardado (Nothing si no lo hay).</summary>
        Public Property ClienteAnterior As String
        ''' <summary>NestoAPI#876: aviso del alta sobre el cliente anterior (p. ej. tiene el mismo NIF). Nothing si no lo hay.</summary>
        Public Property AvisoClienteAnterior As String
        ''' <summary>NestoAPI#876 (corte nuevo): con CopiarDelAnterior, lo que se ha copiado del cliente anterior («Contacto 0: IVA G21 → R52»…).</summary>
        Public Property CopiadoDelAnterior As List(Of String)
        ''' <summary>NestoAPI#876 (corte nuevo): por qué no se ha copiado, o los avisos de la copia (la cuenta bancaria…). Nothing si no hay.</summary>
        Public Property AvisoCopiaClienteAnterior As String
    End Class

End Namespace
