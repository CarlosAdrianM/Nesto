Imports System.Globalization
Imports Nesto.Infrastructure.Models

' NestoAPI#896 (Carlos, 27/11/26, «Enseñarlo ya a Dirección»): lógica PURA de la columna «Riesgo»
' y de la línea de resumen de la pestaña Cartera viva de Remesas. Casa cada efecto candidato con su
' riesgo (por Nº de orden, que es el Id del candidato) y compone la frase con lo SELECCIONADO en la rejilla.
' Solo informa: no toca ni Seleccionado ni Preseleccionado.
Public NotInheritable Class RiesgoImpagosRemesa

    Public Const TRAMO_MENOS_5 As String = "< 5 %"
    Public Const TRAMO_5_15 As String = "5-15 %"
    Public Const TRAMO_15_30 As String = "15-30 %"
    Public Const TRAMO_30_O_MAS As String = "≥ 30 %"

    ' Los textos se componen siempre en español, sea cual sea la cultura del equipo.
    Private Shared ReadOnly Cultura As CultureInfo = CultureInfo.GetCultureInfo("es-ES")

    Private Sub New()
    End Sub

    ''' <summary>
    ''' Pone (o quita) el riesgo de cada candidato. Con <paramref name="respuesta"/> Nothing, o un
    ''' candidato que no viene en ella, el candidato se queda sin riesgo (columna vacía, sin color).
    ''' Casa por Nº de orden; si un efecto viniera sin Nº de orden, por empresa + cliente + documento + efecto.
    ''' </summary>
    Public Shared Sub Casar(candidatos As IEnumerable(Of EfectoCandidatoModel), respuesta As CandidatosRiesgoImpagosModel, Optional empresa As String = Nothing)
        If candidatos Is Nothing Then
            Return
        End If
        Dim efectos = If(respuesta?.Efectos, New List(Of EfectoRiesgoImpagoModel)).Where(Function(e) e IsNot Nothing).ToList()
        Dim porOrden = efectos.Where(Function(e) e.NumOrden <> 0).GroupBy(Function(e) e.NumOrden).ToDictionary(Function(g) g.Key, Function(g) g.First())
        Dim porClave = efectos.Where(Function(e) e.NumOrden = 0).GroupBy(Function(e) Clave(e.Empresa, e.Cliente, e.Documento, e.Efecto)).ToDictionary(Function(g) g.Key, Function(g) g.First())

        For Each candidato In candidatos
            If candidato Is Nothing Then
                Continue For
            End If
            Dim riesgo As EfectoRiesgoImpagoModel = Nothing
            If Not porOrden.TryGetValue(candidato.Id, riesgo) Then
                porClave.TryGetValue(Clave(empresa, candidato.Cliente, candidato.Documento, candidato.Efecto), riesgo)
            End If
            Aplicar(candidato, riesgo)
        Next
    End Sub

    Private Shared Sub Aplicar(candidato As EfectoCandidatoModel, riesgo As EfectoRiesgoImpagoModel)
        Dim probabilidad As Double? = riesgo?.Probabilidad
        candidato.ProbabilidadDevolucion = probabilidad
        candidato.RiesgoTexto = FormatearPorcentaje(probabilidad)
        candidato.NivelRiesgo = Nivel(riesgo?.Tramo, probabilidad)
        Dim motivos = If(riesgo?.Motivos, New List(Of String)).Where(Function(m) Not String.IsNullOrWhiteSpace(m)).Select(Function(m) m.Trim()).ToList()
        candidato.MotivosRiesgo = If(motivos.Any(), String.Join(Environment.NewLine, motivos), Nothing)
    End Sub

    Private Shared Function Clave(empresa As String, cliente As String, documento As String, efecto As String) As String
        Return String.Join("|", {If(empresa, ""), If(cliente, ""), If(documento, ""), If(efecto, "")}.Select(Function(s) s.Trim()))
    End Function

    ''' <summary>0,237 → «23,7 %»; Nothing → Nothing.</summary>
    Public Shared Function FormatearPorcentaje(probabilidad As Double?) As String
        If Not probabilidad.HasValue Then
            Return Nothing
        End If
        Return (probabilidad.Value * 100).ToString("0.0", Cultura) & " %"
    End Function

    ''' <summary>
    ''' El color de la celda: 0 sin color (sin dato o &lt; 5 %), 1 amarillo (5-15 %), 2 naranja (15-30 %),
    ''' 3 rojo (≥ 30 %). Manda el tramo de la API; si no lo trae, los mismos límites sobre la probabilidad.
    ''' </summary>
    Public Shared Function Nivel(tramo As String, probabilidad As Double?) As Integer
        Select Case tramo?.Trim()
            Case TRAMO_MENOS_5
                Return 0
            Case TRAMO_5_15
                Return 1
            Case TRAMO_15_30
                Return 2
            Case TRAMO_30_O_MAS
                Return 3
        End Select
        If Not probabilidad.HasValue Then
            Return 0
        End If
        Dim p = probabilidad.Value
        If p >= 0.3 Then
            Return 3
        ElseIf p >= 0.15 Then
            Return 2
        ElseIf p >= 0.05 Then
            Return 1
        End If
        Return 0
    End Function

    ''' <summary>
    ''' La línea de resumen. Nothing = no se enseña nada (sin respuesta 200). Sin modelo (ConModelo false) y con
    ''' Mensaje, el Mensaje en su lugar. Si no, se calcula sobre los candidatos SELECCIONADOS (los que irían en la
    ''' remesa; ajuste de Carlos 27/11/26: la API suma también los retenidos, que hoy no se giran), con la
    ''' probabilidad que ya les puso <see cref="Casar"/> y el importe que enseña la rejilla. Ejemplo: «Riesgo de
    ''' devolución: se esperan 4,2 devoluciones (1.830,50 € en riesgo). 3 recibos con 30 % o más, 5 entre 15 y 30 %.
    ''' Fuera de la selección hay 2 recibos con 15 % o más.»
    ''' </summary>
    Public Shared Function ComponerResumen(respuesta As CandidatosRiesgoImpagosModel, candidatos As IEnumerable(Of EfectoCandidatoModel)) As String
        If respuesta Is Nothing Then
            Return Nothing
        End If
        Dim mensaje As String = If(String.IsNullOrWhiteSpace(respuesta.Mensaje), Nothing, respuesta.Mensaje.Trim())
        If Not respuesta.ConModelo AndAlso mensaje IsNot Nothing Then
            Return mensaje
        End If

        Dim todos = If(candidatos, Enumerable.Empty(Of EfectoCandidatoModel)()).Where(Function(c) c IsNot Nothing).ToList()
        Dim seleccionados = todos.Where(Function(c) c.Seleccionado).ToList()
        Dim fuera = todos.Where(Function(c) Not c.Seleccionado AndAlso c.NivelRiesgo >= 2).Count()
        Dim fraseFuera = If(fuera = 0, "",
            $" Fuera de la selección hay {fuera} {If(fuera = 1, "recibo", "recibos")} con 15 % o más.")

        If Not seleccionados.Any() Then
            Return "No hay recibos seleccionados." & fraseFuera
        End If

        Dim conProbabilidad = seleccionados.Where(Function(c) c.ProbabilidadDevolucion.HasValue).ToList()
        Dim esperadas As Double = conProbabilidad.Sum(Function(c) c.ProbabilidadDevolucion.Value)
        Dim importeEnRiesgo As Decimal = Math.Round(
            conProbabilidad.Sum(Function(c) c.ImportePendiente * CDec(c.ProbabilidadDevolucion.Value)), 2, MidpointRounding.AwayFromZero)

        Dim textoEsperadas = Math.Round(esperadas, 1, MidpointRounding.AwayFromZero).ToString("0.0", Cultura)
        Dim frase = If(textoEsperadas = "1,0",
            $"Riesgo de devolución: se espera {textoEsperadas} devolución",
            $"Riesgo de devolución: se esperan {textoEsperadas} devoluciones")
        frase &= $" ({importeEnRiesgo.ToString("C", Cultura)} en riesgo)."

        Dim altos = seleccionados.Where(Function(c) c.NivelRiesgo = 3).Count()
        Dim medios = seleccionados.Where(Function(c) c.NivelRiesgo = 2).Count()
        If altos = 0 AndAlso medios = 0 Then
            Return frase & " Ningún recibo con 15 % o más." & fraseFuera
        End If
        Dim partes As New List(Of String)
        If altos > 0 Then
            partes.Add($"{altos} {If(altos = 1, "recibo", "recibos")} con 30 % o más")
        End If
        If medios > 0 Then
            partes.Add($"{medios} {If(altos > 0, "", If(medios = 1, "recibo ", "recibos "))}entre 15 y 30 %")
        End If
        Return frase & " " & String.Join(", ", partes) & "." & fraseFuera
    End Function

End Class
