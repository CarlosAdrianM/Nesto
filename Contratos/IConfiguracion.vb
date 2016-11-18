Public Interface IConfiguracion
    ' Dirección IP del servidor con NestoAPI
    ReadOnly Property servidorAPI As String
    Function leerParametro(empresa As String, clave As String) As Task(Of String)

End Interface
