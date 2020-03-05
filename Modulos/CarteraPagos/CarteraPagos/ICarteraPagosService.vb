Public Interface ICarteraPagosService
    Function CrearFichero(numeroRemesa As Integer) As Task(Of String)
    Function CrearFichero(extractoId As Integer, numeroBanco As String) As Task(Of String)
End Interface
