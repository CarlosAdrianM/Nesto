Public Interface IConfiguracion
    ' Dirección IP del servidor con NestoAPI
    Function leerParametro(empresa As String, clave As String) As Task(Of String)
    ReadOnly Property servidorAPI As String
    ReadOnly Property usuario As String

End Interface
