Public Class Constantes
    Public Class Agencias
        Public Const AGENCIA_DEFECTO As String = "Correos Express"
        Public Const AGENCIA_INTERNACIONAL As String = "Correos Express" ' String.Empty para no usar ninguna
        Public Const AGENCIA_REEMBOLSOS As String = "Sending" ' String.Empty para no usar ninguna
        Public Const ESTADO_INICIAL_ENVIO = 0
        Public Const ESTADO_TRAMITADO_ENVIO = 1
        Public Const ESTADO_PENDIENTE_ENVIO As Int16 = -1 ' Etiqueta pendiente de envío
    End Class
    Public Class Clientes
        Public Const ESTADO_NORMAL = 0
        Public Const ESTADO_DISTRIBUIDOR = 6
        Public Class Especiales
            Public Const TIENDA_ONLINE As String = "31517"
            Public Const AMAZON As String = "32624"
            Public Const EL_EDEN As String = "15191"
        End Class
    End Class
    Public Class DiariosContables
        Public Const DIARIO_REEMBOLSOS As String = "_Reembolso"
        Public Const DIARIO_PAGO_REEMBOLSOS As String = "_PagoReemb"
    End Class
    Public Class Empresas
        Public Const EMPRESA_DEFECTO As String = "1"
        Public Const EMPRESA_ESPEJO As String = "3  "
        Public Const DELEGACION_DEFECTO As String = "ALG"
        Public Const FORMA_VENTA_DEFECTO As String = "VAR"
        Public Const MONEDA_CONTABILIDAD As String = "EUR"
    End Class
    Public Class Familias
        Public Const UNION_LASER_NOMBRE As String = "Unión Láser"
    End Class
    Public Class FormasPago
        Public Const EFECTIVO As String = "EFC"
        Public Const RECIBO As String = "RCB"
        Public Const TARJETA As String = "TAR"
    End Class
    Public Class FormasVenta
        Public Shared ReadOnly FORMAS_ONLINE As New List(Of String) From {"QRU", "WEB", "STK", "BLT"}
    End Class
    Public Class GruposSeguridad
        Public Const ADMINISTRACION As String = "Administración"
        Public Const ALMACEN As String = "Almacén"
        Public Const DIRECCION As String = "Dirección"
        Public Const FACTURACION As String = "Facturación"
        Public Const TIENDA_ON_LINE As String = "TiendaOnline"
    End Class
    Public Class LineasPedido
        Public Const ESTADO_SIN_FACTURAR = 1
        Public Const ESTADO_LINEA_PENDIENTE = -1
    End Class
    Public Class PlazosPago
        Public Const CONTADO As String = "CONTADO"
        Public Const PREPAGO As String = "PRE"
    End Class
    Public Class Series
        Public Const SERIE_CURSOS As String = "CV"
        Public Const SERIE_DEFECTO As String = "NV"
        Public Const UNION_LASER As String = "UL"
    End Class
    Public Class TiposApunte
        Public Const PASO_A_CARTERA = "0"
        Public Const FACTURA = "1"
        Public Const CARTERA = "2"
        Public Const PAGO = "3"
        Public Const IMPAGADO = "4"
    End Class
    Public Class TiposCuenta
        Public Const CUENTA_CONTABLE = "1"
        Public Const CLIENTE = "2"
        Public Const PROVEEDOR = "3"
    End Class
End Class
