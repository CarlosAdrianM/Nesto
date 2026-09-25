''' <summary>
''' Nesto#493: lo que un selector de modos necesita de la respuesta de la API, sea de modos de servicio
''' (<see cref="ModoServicioSugeridoDTO"/>) o de facturación (<see cref="ModoFacturacionSugeridoDTO"/>):
''' qué modos se pueden elegir y por qué no los demás.
''' </summary>
Public Interface ISugerenciaModos
    ''' <summary>Los modos que se pueden elegir. Vacío o Nothing (API anterior) = todos.</summary>
    ReadOnly Property ModosPermitidos As List(Of Byte)
    ''' <summary>Por qué no vale un modo, según la API (Nothing si no lo dice o si vale).</summary>
    Function MotivoDe(modo As Byte) As String
End Interface
