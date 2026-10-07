Imports ControlesUsuario.Models

Public Class ClienteProbabilidadVenta
    Inherits ClienteDTO

    Public Property Probabilidad As Single
    Public Property DiasDesdeUltimoPedido As Integer
    Public Property DiasDesdeUltimaInteraccion As Integer

    ' NestoAPI#603: campos de GET api/Clientes/SugerenciasContacto. Con el endpoint antiguo
    ' (GetClientesProbabilidadVenta) llegan vacíos y la tarjeta se pinta sin prioridad.
    Public Property SugerenciaId As Integer?
    ''' <summary>"Máxima" | "Alta" | "Media" | "Baja" (con tilde), o Nothing con el endpoint antiguo.</summary>
    Public Property Prioridad As String
    ''' <summary>1, 2, 3… en el orden en que conviene llamar.</summary>
    Public Property Orden As Integer
    Public Property Motivo As String
    Public Property CadenciaDias As Integer?
    ''' <summary>Nothing si no consta ningún contacto.</summary>
    Public Property DiasDesdeUltimoContacto As Integer?
    Public Property PedidosUltimos12Meses As Integer?
    Public Property ImporteUltimos12Meses As Decimal?
    Public Property GrupoSubgrupoMasVendido As String
    Public Property Atendida As Boolean

    Public ReadOnly Property TienePrioridad As Boolean
        Get
            Return Not String.IsNullOrWhiteSpace(Prioridad)
        End Get
    End Property

    Public ReadOnly Property TieneMotivo As Boolean
        Get
            Return Not String.IsNullOrWhiteSpace(Motivo)
        End Get
    End Property

    Public ReadOnly Property DiasDesdeUltimoPedidoInteraccionTexto As String
        Get
            Dim textoContacto As String
            If TienePrioridad Then
                textoContacto = If(DiasDesdeUltimoContacto.HasValue,
                    $"{DiasDesdeUltimoContacto.Value} días desde el último contacto.",
                    "No consta ningún contacto.")
            Else
                textoContacto = $"{DiasDesdeUltimaInteraccion} días desde la última interacción."
            End If
            Dim texto As String = $"{DiasDesdeUltimoPedido} días desde el último pedido.{vbCr}{textoContacto}"
            If CadenciaDias.HasValue Then
                texto &= $"{vbCr}Cadencia: un contacto cada {CadenciaDias.Value} días."
            End If
            If PedidosUltimos12Meses.HasValue Then
                texto &= $"{vbCr}{PedidosUltimos12Meses.Value} pedidos en 12 meses"
                If ImporteUltimos12Meses.HasValue Then
                    texto &= $" ({ImporteUltimos12Meses.Value:N2} €)"
                End If
                texto &= "."
            End If
            If TieneMotivo Then
                texto = Motivo & vbCr & texto
            End If
            If Atendida Then
                texto &= $"{vbCr}Ya atendida hoy."
            End If
            Return texto
        End Get
    End Property
End Class
