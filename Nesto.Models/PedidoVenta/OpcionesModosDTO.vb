''' <summary>
''' NestoAPI#688: lo que contesta POST api/PedidosVenta/OpcionesModos, la regla única que cruza modo de servicio ×
''' modo de facturación × «Servir lo pendiente en cuanto haya stock» y dice qué se enseña en pantalla. Sustituye a
''' las dos preguntas de antes (ModoServicioSugerido y ModoFacturacionSugerido), que siguen como envoltorios en la API.
''' El cliente no interpreta nada: lo que no tiene sentido no se muestra.
''' </summary>
Public Class OpcionesModosDTO
    ''' <summary>El modo de servicio sugerido y los permitidos (lo mismo que ModoServicioSugerido).</summary>
    Public Property Servicio As ModoServicioSugeridoDTO
    ''' <summary>El modo de facturación que rige, los permitidos y si el combo se enseña (con «Todo junto», no).</summary>
    Public Property Facturacion As OpcionesFacturacionDTO
    ''' <summary>La casilla «Servir lo pendiente en cuanto haya stock» (solo con «Todo ahora»).</summary>
    Public Property ServirPendienteAlHaberStock As OpcionServirPendienteDTO

    ''' <summary>True si la respuesta se ha montado con los dos endpoints viejos porque la API todavía no tiene
    ''' OpcionesModos (404): entonces el combo «Facturación» se enseña siempre y la casilla no.</summary>
    Public Property EsApiAnterior As Boolean

    ''' <summary>
    ''' Compatibilidad con una API anterior a NestoAPI#688: la misma respuesta con lo que contestan los dos endpoints
    ''' viejos, sin casilla y con el combo «Facturación» siempre visible (el comportamiento de antes).
    ''' </summary>
    Public Shared Function DesdeApiAnterior(servicio As ModoServicioSugeridoDTO, facturacion As ModoFacturacionSugeridoDTO) As OpcionesModosDTO
        Return New OpcionesModosDTO With {
            .Servicio = servicio,
            .Facturacion = OpcionesFacturacionDTO.Desde(facturacion),
            .ServirPendienteAlHaberStock = New OpcionServirPendienteDTO With {.Visible = False},
            .EsApiAnterior = True
        }
    End Function
End Class

''' <summary>NestoAPI#688: el bloque «Facturación» de OpcionesModos. Lo de ModoFacturacionSugerido más si se enseña.</summary>
Public Class OpcionesFacturacionDTO
    Inherits ModoFacturacionSugeridoDTO

    ''' <summary>False con «Todo junto»: el combo se oculta y rige lo que pongan los triggers (decisión 6).
    ''' Si la API no lo manda, se enseña (como antes).</summary>
    Public Property Visible As Boolean = True

    ''' <summary>Nothing si no hay sugerencia; si no, la misma con el combo visible.</summary>
    Public Shared Function Desde(sugerencia As ModoFacturacionSugeridoDTO) As OpcionesFacturacionDTO
        If sugerencia Is Nothing Then
            Return Nothing
        End If
        Dim opciones = TryCast(sugerencia, OpcionesFacturacionDTO)
        If opciones IsNot Nothing Then
            Return opciones
        End If
        Return New OpcionesFacturacionDTO With {
            .Modo = sugerencia.Modo,
            .Nombre = sugerencia.Nombre,
            .Motivo = sugerencia.Motivo,
            .ModosPermitidos = sugerencia.ModosPermitidos,
            .Modos = sugerencia.Modos,
            .Visible = True
        }
    End Function
End Class

''' <summary>NestoAPI#688: la casilla «Servir lo pendiente en cuanto haya stock».</summary>
Public Class OpcionServirPendienteDTO
    ''' <summary>Solo con facturación «Todo ahora» (modo 3).</summary>
    Public Property Visible As Boolean
    ''' <summary>El valor que propone la API: el guardado o, al elegir «Todo ahora», marcada (decisión 12).</summary>
    Public Property Valor As Boolean?
    ''' <summary>Explicación para el tooltip (Nothing si no la hay).</summary>
    Public Property Motivo As String
End Class
