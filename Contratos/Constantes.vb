Public Class Constantes
    Public Class Agencias
        Public Const AGENCIA_INTERNACIONAL As String = "Correos Express" ' String.Empty para no usar ninguna
        Public Const AGENCIA_REEMBOLSOS As String = "Correos Express" ' String.Empty para no usar ninguna
        Public Const ESTADO_INICIAL_ENVIO = 0
        Public Const ESTADO_TRAMITADO_ENVIO = 1
        Public Const ESTADO_PENDIENTE_ENVIO As Int16 = -1 ' Etiqueta pendiente de envío
    End Class
    Public Class Clientes
        Public Const ESTADO_NORMAL = 0
    End Class
    Public Class DiariosContables
        Public Const DIARIO_REEMBOLSOS As String = "_Reembolso"
    End Class
    Public Class Empresas
        Public Const EMPRESA_ESPEJO As String = "3  "
    End Class
    Public Class FormasPago
        Public Const EFECTIVO As String = "EFC"
    End Class
    Public Class FormasVenta
        Public Shared ReadOnly FORMAS_ONLINE As New List(Of String) From {"QRU", "WEB", "STK", "BLT"}
    End Class
    Public Class GruposSeguridad
        Public Const ADMINISTRACION As String = "Administración"
    End Class
    Public Class LineasPedido
        Public Const ESTADO_SIN_FACTURAR = 1
        Public Const ESTADO_LINEA_PENDIENTE = -1
    End Class
End Class
