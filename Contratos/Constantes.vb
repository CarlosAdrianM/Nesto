﻿Public Class Constantes
    Public Class Agencias
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
    Public Class LineasPedido
        Public Const ESTADO_SIN_FACTURAR = 1
        Public Const ESTADO_LINEA_PENDIENTE = -1
    End Class
End Class