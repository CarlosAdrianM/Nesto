''' <summary>
''' NestoAPI#603: respuesta de GET api/Clientes/SugerenciasContacto. Cada sugerencia deserializa
''' tal cual en <see cref="ClienteProbabilidadVenta"/>. Ritmo es Nothing cuando se ha caído al
''' endpoint antiguo (API sin publicar).
''' </summary>
Public Class SugerenciasContactoRespuesta
    Public Property Vendedor As String
    Public Property Fecha As Date?
    Public Property Ritmo As RitmoContactoDTO
    Public Property Sugerencias As List(Of ClienteProbabilidadVenta)
End Class

Public Class RitmoContactoDTO
    Public Property ContactosHoy As Integer
    Public Property ContactosSemana As Integer
    Public Property ContactosMes As Integer
    Public Property ObjetivoMes As Integer
    Public Property ObjetivoHoy As Integer
    Public Property DiasLaborablesRestantesMes As Integer
    Public Property PendientesMaxima As Integer
    Public Property PendientesAlta As Integer
    Public Property PendientesMedia As Integer
    Public Property PendientesBaja As Integer
    Public Property Frase As String

    Public ReadOnly Property TextoContactos As String
        Get
            Return $"Hoy {ContactosHoy} · Semana {ContactosSemana} · Mes {ContactosMes} de {ObjetivoMes}"
        End Get
    End Property

    Public ReadOnly Property TextoObjetivoHoy As String
        Get
            Return $"Objetivo hoy: {ObjetivoHoy}"
        End Get
    End Property

    Public ReadOnly Property TextoPendientes As String
        Get
            Return $"Máxima {PendientesMaxima} · Alta {PendientesAlta} · Media {PendientesMedia} · Baja {PendientesBaja}"
        End Get
    End Property

    ''' <summary>Avance del mes de 0 a 100 (para la barra); 100 si ya se ha llegado o no hay objetivo.</summary>
    Public ReadOnly Property PorcentajeMes As Double
        Get
            If ObjetivoMes <= 0 Then
                Return 100
            End If
            Return Math.Min(100, ContactosMes * 100.0 / ObjetivoMes)
        End Get
    End Property

    Public ReadOnly Property TieneFrase As Boolean
        Get
            Return Not String.IsNullOrWhiteSpace(Frase)
        End Get
    End Property
End Class
