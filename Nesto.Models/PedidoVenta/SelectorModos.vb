''' <summary>
''' Nesto#484 / NestoAPI#518 y Nesto#493 / NestoAPI#542: el núcleo común de los combos de modos del pedido
''' («Servir» y «Facturación», en la plantilla y en el detalle). La API decide qué modos tienen sentido
''' (<see cref="ISugerenciaModos.ModosPermitidos"/>); aquí solo se refleja: las opciones que no valen se
''' deshabilitan con el motivo de la API y, cuando el modo elegido deja de valer, se redacta el aviso
''' (Carlos: nada de cambiar el modo en silencio). Lo específico de cada selector es su lista de modos:
''' <see cref="SelectorModosServicio"/> y <see cref="SelectorModosFacturacion"/>.
''' </summary>
Public Class SelectorModos
    Private ReadOnly _lista As IReadOnlyList(Of ModoItem)

    Public Sub New(lista As IReadOnlyList(Of ModoItem))
        _lista = lista
        Opciones = lista.Select(Function(m) New OpcionModo(m)).ToList()
    End Sub

    ''' <summary>Las opciones, en el orden de la lista del modo. Sin respuesta, todas habilitadas.</summary>
    Public ReadOnly Property Opciones As IReadOnlyList(Of OpcionModo)

    ''' <summary>Habilita/deshabilita las opciones con lo que manda la API (Nothing o sin lista = todas).</summary>
    Public Sub Aplicar(sugerencia As ISugerenciaModos)
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
    Public Shared Function MotivoDe(sugerencia As ISugerenciaModos, modo As Byte) As String
        Return sugerencia?.MotivoDe(modo)
    End Function

    ''' <summary>El aviso que se enseña al pasar de un modo que ya no tiene sentido al que sí.</summary>
    Public Function TextoAvisoCambio(anterior As Byte, nuevo As Byte, motivo As String) As String
        Return TextoAvisoCambio(_lista, anterior, nuevo, motivo)
    End Function

    Public Shared Function TextoAvisoCambio(lista As IReadOnlyList(Of ModoItem), anterior As Byte, nuevo As Byte, motivo As String) As String
        Dim nombreAnterior As String = lista.FirstOrDefault(Function(m) m.Codigo = anterior)?.Nombre
        Dim nombreNuevo As String = lista.FirstOrDefault(Function(m) m.Codigo = nuevo)?.Nombre
        Return $"«{nombreAnterior}» ya no tiene sentido para este pedido" &
            If(String.IsNullOrWhiteSpace(motivo), String.Empty, $" ({motivo.TrimEnd("."c)})") &
            $": se ha cambiado a «{nombreNuevo}»."
    End Function
End Class
