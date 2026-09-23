''' <summary>
''' Nesto#484 / NestoAPI#518: lo común a los dos combos «Servir» (plantilla y detalle de pedido). La API decide
''' qué modos tienen sentido (<see cref="ModoServicioSugeridoDTO.ModosPermitidos"/>); aquí solo se refleja:
''' las opciones que no valen se deshabilitan con el motivo de la API y, cuando el modo elegido deja de valer,
''' se redacta el aviso (Carlos: nada de cambiar el modo en silencio).
''' </summary>
Public Class SelectorModosServicio
    ''' <summary>Las cuatro opciones, en el orden de <see cref="ModosServicio.Lista"/>. Sin respuesta, todas habilitadas.</summary>
    Public ReadOnly Property Opciones As IReadOnlyList(Of OpcionModoServicio) =
        ModosServicio.Lista.Select(Function(m) New OpcionModoServicio(m)).ToList()

    ''' <summary>Habilita/deshabilita las opciones con lo que manda la API (Nothing o sin lista = todas).</summary>
    Public Sub Aplicar(sugerencia As ModoServicioSugeridoDTO)
        Dim permitidos As List(Of Byte) = sugerencia?.ModosPermitidos
        Dim hayRestriccion As Boolean = permitidos IsNot Nothing AndAlso permitidos.Any()
        For Each opcion In Opciones
            opcion.Habilitado = Not hayRestriccion OrElse permitidos.Contains(opcion.Codigo)
            opcion.MotivoNoPermitido = If(opcion.Habilitado, Nothing, MotivoDe(sugerencia, opcion.Codigo))
        Next
    End Sub

    ''' <summary>¿Se puede elegir este modo con lo que dijo la API la última vez?</summary>
    Public Function EsPermitido(modo As Byte) As Boolean
        Return Opciones.Any(Function(o) o.Codigo = modo AndAlso o.Habilitado)
    End Function

    ''' <summary>Por qué no vale un modo, según la API (Nothing si no lo dice).</summary>
    Public Shared Function MotivoDe(sugerencia As ModoServicioSugeridoDTO, modo As Byte) As String
        Return sugerencia?.Modos?.FirstOrDefault(Function(m) m.Modo = modo)?.Motivo
    End Function

    ''' <summary>El aviso que se enseña al pasar de un modo que ya no tiene sentido al que sí.</summary>
    Public Shared Function TextoAvisoCambio(anterior As Byte, nuevo As Byte, motivo As String) As String
        Dim nombreAnterior As String = ModosServicio.Lista.FirstOrDefault(Function(m) m.Codigo = anterior)?.Nombre
        Dim nombreNuevo As String = ModosServicio.Lista.FirstOrDefault(Function(m) m.Codigo = nuevo)?.Nombre
        Return $"«{nombreAnterior}» ya no tiene sentido para este pedido" &
            If(String.IsNullOrWhiteSpace(motivo), String.Empty, $" ({motivo.TrimEnd("."c)})") &
            $": se ha cambiado a «{nombreNuevo}»."
    End Function
End Class
