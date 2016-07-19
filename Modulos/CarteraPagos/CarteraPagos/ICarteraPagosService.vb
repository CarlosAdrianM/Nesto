Public Interface ICarteraPagosService
    Function crearFichero(empresa As String, numeroRemesa As Integer) As Task(Of String)
End Interface
