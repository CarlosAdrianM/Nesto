Public Class PlantillaVentaModel

    Public Class ClienteJson
        Public Property empresa() As String
            Get
                Return m_empresa
            End Get
            Set
                m_empresa = Value
            End Set
        End Property
        Private m_empresa As String
        Public Property cliente() As String
            Get
                Return m_cliente
            End Get
            Set
                m_cliente = Value
            End Set
        End Property
        Private m_cliente As String
        Public Property contacto() As String
            Get
                Return m_contacto
            End Get
            Set
                m_contacto = Value
            End Set
        End Property
        Private m_contacto As String
        Public Property clientePrincipal() As Boolean
            Get
                Return m_clientePrincipal
            End Get
            Set
                m_clientePrincipal = Value
            End Set
        End Property
        Private m_clientePrincipal As Boolean
        Public Property nombre() As String
            Get
                Return m_nombre
            End Get
            Set
                m_nombre = Value
            End Set
        End Property
        Private m_nombre As String
        Public Property direccion() As String
            Get
                Return m_direccion
            End Get
            Set
                m_direccion = Value
            End Set
        End Property
        Private m_direccion As String
        Public Property poblacion() As String
            Get
                Return m_poblacion
            End Get
            Set
                m_poblacion = Value
            End Set
        End Property
        Private m_poblacion As String
        Public Property telefono() As String
            Get
                Return m_telefono
            End Get
            Set
                m_telefono = Value
            End Set
        End Property
        Private m_telefono As String
        Public Property vendedor() As String
            Get
                Return m_vendedor
            End Get
            Set
                m_vendedor = Value
            End Set
        End Property
        Private m_vendedor As String
        Public Property comentarios() As String
            Get
                Return m_comentarios
            End Get
            Set
                m_comentarios = Value
            End Set
        End Property
        Private m_comentarios As String
        Public Property codigoPostal() As String
            Get
                Return m_codigoPostal
            End Get
            Set
                m_codigoPostal = Value
            End Set
        End Property
        Private m_codigoPostal As String
        Public Property cifNif() As String
            Get
                Return m_cifNif
            End Get
            Set
                m_cifNif = Value
            End Set
        End Property
        Private m_cifNif As String
        Public Property provincia() As String
            Get
                Return m_provincia
            End Get
            Set
                m_provincia = Value
            End Set
        End Property
        Private m_provincia As String
        Public Property estado() As Integer
            Get
                Return m_estado
            End Get
            Set
                m_estado = Value
            End Set
        End Property
        Private m_estado As Integer
        Public Property iva() As String
            Get
                Return m_iva
            End Get
            Set
                m_iva = Value
            End Set
        End Property
        Private m_iva As String
        Public Property grupo() As String
            Get
                Return m_grupo
            End Get
            Set
                m_grupo = Value
            End Set
        End Property
        Private m_grupo As String
        Public Property periodoFacturacion() As String
            Get
                Return m_periodoFacturacion
            End Get
            Set
                m_periodoFacturacion = Value
            End Set
        End Property
        Private m_periodoFacturacion As String
        Public Property ccc() As String
            Get
                Return m_ccc
            End Get
            Set
                m_ccc = Value
            End Set
        End Property
        Private m_ccc As String
        Public Property ruta() As String
            Get
                Return m_ruta
            End Get
            Set
                m_ruta = Value
            End Set
        End Property
        Private m_ruta As String
        Public Property copiasAlbaran() As Integer
            Get
                Return m_copiasAlbaran
            End Get
            Set
                m_copiasAlbaran = Value
            End Set
        End Property
        Private m_copiasAlbaran As Integer
        Public Property copiasFactura() As Integer
            Get
                Return m_copiasFactura
            End Get
            Set
                m_copiasFactura = Value
            End Set
        End Property
        Private m_copiasFactura As Integer
        Public Property comentarioRuta() As Object
            Get
                Return m_comentarioRuta
            End Get
            Set
                m_comentarioRuta = Value
            End Set
        End Property
        Private m_comentarioRuta As Object
        Public Property comentarioPicking() As Object
            Get
                Return m_comentarioPicking
            End Get
            Set
                m_comentarioPicking = Value
            End Set
        End Property
        Private m_comentarioPicking As Object
        Public Property web() As String
            Get
                Return m_web
            End Get
            Set
                m_web = Value
            End Set
        End Property
        Private m_web As String
        Public Property albaranValorado() As Boolean
            Get
                Return m_albaranValorado
            End Get
            Set
                m_albaranValorado = Value
            End Set
        End Property
        Private m_albaranValorado As Boolean
        Public Property cadena() As Object
            Get
                Return m_cadena
            End Get
            Set
                m_cadena = Value
            End Set
        End Property
        Private m_cadena As Object
        Public Property noComisiona() As Double
            Get
                Return m_noComisiona
            End Get
            Set
                m_noComisiona = Value
            End Set
        End Property
        Private m_noComisiona As Double
        Public Property servirJunto() As Boolean
            Get
                Return m_servirJunto
            End Get
            Set
                m_servirJunto = Value
            End Set
        End Property
        Private m_servirJunto As Boolean
        Public Property mantenerJunto() As Boolean
            Get
                Return m_mantenerJunto
            End Get
            Set
                m_mantenerJunto = Value
            End Set
        End Property
        Private m_mantenerJunto As Boolean

        Public ReadOnly Property rutaLogo As String
            Get
                If empresa = "1" OrElse empresa = "2" OrElse empresa = "4" OrElse empresa = "5" Then
                    Return "pack://application:,,,/PlantillaVenta;component/Images/logo" + empresa + ".jpg"
                Else
                    Return ""
                End If
            End Get
        End Property

        Public ReadOnly Property colorEstado As Brush
            Get
                If estado = -1 Then
                    Return Brushes.Red
                ElseIf estado = 7 Then
                    Return Brushes.Orange
                ElseIf estado = 0 OrElse estado = 9
                    Return Brushes.Green
                Else
                    Return Brushes.Black
                End If
            End Get
        End Property
    End Class
    Public Class LineaPlantillaJson
        Public Property producto() As String
            Get
                Return m_producto
            End Get
            Set
                m_producto = Value
            End Set
        End Property
        Private m_producto As String
        Public Property texto() As String
            Get
                Return m_texto
            End Get
            Set
                m_texto = Value
            End Set
        End Property
        Private m_texto As String
        Public Property cantidad() As Integer
            Get
                Return m_cantidad
            End Get
            Set
                m_cantidad = Value
            End Set
        End Property
        Private m_cantidad As Integer
        Public Property cantidadOferta() As Integer
            Get
                Return m_cantidadOferta
            End Get
            Set
                m_cantidadOferta = Value
            End Set
        End Property
        Private m_cantidadOferta As Integer
        Public Property tamanno() As System.Nullable(Of Integer)
            Get
                Return m_tamanno
            End Get
            Set
                m_tamanno = Value
            End Set
        End Property
        Private m_tamanno As System.Nullable(Of Integer)
        Public Property unidadMedida() As String
            Get
                Return m_unidadMedida
            End Get
            Set
                m_unidadMedida = Value
            End Set
        End Property
        Private m_unidadMedida As String
        Public Property familia() As String
            Get
                Return m_familia
            End Get
            Set
                m_familia = Value
            End Set
        End Property
        Private m_familia As String
        Public Property subGrupo() As String
            Get
                Return m_subGrupo
            End Get
            Set
                m_subGrupo = Value
            End Set
        End Property
        Private m_subGrupo As String
        Public Property estado() As Integer
            Get
                Return m_estado
            End Get
            Set
                m_estado = Value
            End Set
        End Property
        Private m_estado As Integer
        Public Property yaFacturado() As Boolean
            Get
                Return m_yaFacturado
            End Get
            Set
                m_yaFacturado = Value
            End Set
        End Property
        Private m_yaFacturado As Boolean
        Public Property cantidadVendida() As Integer
            Get
                Return m_cantidadVendida
            End Get
            Set
                m_cantidadVendida = Value
            End Set
        End Property
        Private m_cantidadVendida As Integer
        Public Property cantidadAbonada() As Integer
            Get
                Return m_cantidadAbonada
            End Get
            Set
                m_cantidadAbonada = Value
            End Set
        End Property
        Private m_cantidadAbonada As Integer

        Public Property fechaUltimaVenta() As System.Nullable(Of DateTime)
            Get
                Return m_fechaUltimaVenta
            End Get
            Set
                m_fechaUltimaVenta = Value
            End Set
        End Property
        Private m_fechaUltimaVenta As System.Nullable(Of DateTime)

        Public ReadOnly Property colorEstado As Brush
            Get
                If producto = "18150" Then
                    producto = producto
                End If
                If cantidadAbonada >= cantidadVendida Then
                    Return Brushes.Red
                ElseIf fechaUltimaVenta < DateAdd(DateInterval.Year, -1, Now)
                    Return Brushes.Orange
                ElseIf fechaUltimaVenta < DateAdd(DateInterval.Month, -6, Now) AndAlso (cantidadAbonada = 0 OrElse cantidadVendida / cantidadAbonada > 10)
                    Return Brushes.Yellow
                ElseIf fechaUltimaVenta < DateAdd(DateInterval.Month, -2, Now) AndAlso (cantidadAbonada = 0 OrElse cantidadVendida / cantidadAbonada > 10)
                    Return Brushes.YellowGreen
                ElseIf Not IsNothing(fechaUltimaVenta) Then
                    Return Brushes.Green
                Else
                    Return Brushes.Blue
                End If
            End Get
        End Property

        Public ReadOnly Property textoUnidadesVendidas As String
            Get
                If cantidadAbonada = 0 And cantidadVendida > 1 Then
                    Return String.Format("Vendidas {0} unds.", cantidadVendida)
                ElseIf cantidadAbonada = 0 And cantidadVendida = 1 Then
                    Return "Vendida 1 und."
                ElseIf fechaUltimaVenta = DateTime.MinValue Then
                    Return ""
                ElseIf (cantidadVendida = 0 AndAlso cantidadAbonada = 0)
                    Return "Ninguna unidad vendida"
                Else
                    Return String.Format("Vendidas {0} unds. (facturadas {1} y abonadas {2}).", cantidadVendida - cantidadAbonada, cantidadVendida, cantidadAbonada)
                End If
            End Get
        End Property

        Public ReadOnly Property textoFechaUltimaVenta As String
            Get
                If IsNothing(fechaUltimaVenta) OrElse fechaUltimaVenta = DateTime.MinValue Then
                    Return ""
                Else
                    Return fechaUltimaVenta.ToString
                End If
            End Get
        End Property

        Public ReadOnly Property textoNombreProducto As String
            Get
                Dim textoCompleto As String = texto
                If tamanno <> 0 Then
                    textoCompleto = textoCompleto.Trim + " " + CType(tamanno, String)
                End If
                If unidadMedida <> "" Then
                    textoCompleto = textoCompleto.Trim + " " + unidadMedida
                End If
                Return textoCompleto
            End Get
        End Property

    End Class
    Public Class DireccionesEntregaJson
        Public Property contacto() As String
            Get
                Return m_contacto
            End Get
            Set
                m_contacto = Value
            End Set
        End Property
        Private m_contacto As String
        Public Property clientePrincipal() As Boolean
            Get
                Return m_clientePrincipal
            End Get
            Set
                m_clientePrincipal = Value
            End Set
        End Property
        Private m_clientePrincipal As Boolean
        Public Property nombre() As String
            Get
                Return m_nombre
            End Get
            Set
                m_nombre = Value
            End Set
        End Property
        Private m_nombre As String
        Public Property direccion() As String
            Get
                Return m_direccion
            End Get
            Set
                m_direccion = Value
            End Set
        End Property
        Private m_direccion As String
        Public Property poblacion() As String
            Get
                Return m_poblacion
            End Get
            Set
                m_poblacion = Value
            End Set
        End Property
        Private m_poblacion As String
        Public Property comentarios() As String
            Get
                Return m_comentarios
            End Get
            Set
                m_comentarios = Value
            End Set
        End Property
        Private m_comentarios As String
        Public Property codigoPostal() As String
            Get
                Return m_codigoPostal
            End Get
            Set
                m_codigoPostal = Value
            End Set
        End Property
        Private m_codigoPostal As String
        Public Property provincia() As String
            Get
                Return m_provincia
            End Get
            Set
                m_provincia = Value
            End Set
        End Property
        Private m_provincia As String
        Public Property estado() As Integer
            Get
                Return m_estado
            End Get
            Set
                m_estado = Value
            End Set
        End Property
        Private m_estado As Integer
        Public Property iva() As String
            Get
                Return m_iva
            End Get
            Set
                m_iva = Value
            End Set
        End Property
        Private m_iva As String
        Public Property comentarioRuta() As String
            Get
                Return m_comentarioRuta
            End Get
            Set
                m_comentarioRuta = Value
            End Set
        End Property
        Private m_comentarioRuta As String
        Public Property comentarioPicking() As Object
            Get
                Return m_comentarioPicking
            End Get
            Set
                m_comentarioPicking = Value
            End Set
        End Property
        Private m_comentarioPicking As Object
        Public Property noComisiona() As Double
            Get
                Return m_noComisiona
            End Get
            Set
                m_noComisiona = Value
            End Set
        End Property
        Private m_noComisiona As Double
        Public Property servirJunto() As Boolean
            Get
                Return m_servirJunto
            End Get
            Set
                m_servirJunto = Value
            End Set
        End Property
        Private m_servirJunto As Boolean
        Public Property mantenerJunto() As Boolean
            Get
                Return m_mantenerJunto
            End Get
            Set
                m_mantenerJunto = Value
            End Set
        End Property
        Private m_mantenerJunto As Boolean

        Public Property esDireccionPorDefecto() As Boolean
            Get
                Return m_esDireccionPorDefecto
            End Get
            Set
                m_esDireccionPorDefecto = Value
            End Set
        End Property
        Private m_esDireccionPorDefecto As Boolean


        Public ReadOnly Property textoPoblacion As String
            Get
                Return String.Format("{0} {1} ({2})", codigoPostal, poblacion, provincia)
            End Get
        End Property
    End Class
    Public Class LineaPedidoVentaDTO
        Public Property almacen() As String

        Public Property aplicarDescuento() As Boolean

        Public Property cantidad() As Short

        ' era Nullable<short> 
        Public Property delegacion() As String

        Public Property descuento() As Decimal

        Public Property estado() As Short

        Public Property fechaEntrega() As System.DateTime

        Public Property formaVenta() As String

        Public Property iva() As String

        Public Property precio() As Decimal

        ' era Nullable<decimal> 
        Public Property producto() As String

        Public Property texto() As String

        Public Property tipoLinea() As Nullable(Of Byte)

        Public Property usuario() As String

        Public Property vistoBueno() As Boolean

        Public Function ShallowCopy() As LineaPedidoVentaDTO
            Return DirectCast(Me.MemberwiseClone(), LineaPedidoVentaDTO)
        End Function
    End Class

    Public Class PedidoVentaDTO
        Public Sub New()
            Me.LineasPedido = New HashSet(Of LineaPedidoVentaDTO)()
        End Sub

        Public Property empresa() As String
            Get
                Return m_empresa
            End Get
            Set
                m_empresa = Value
            End Set
        End Property
        Private m_empresa As String
        Public Property numero() As Integer
            Get
                Return m_numero
            End Get
            Set
                m_numero = Value
            End Set
        End Property
        Private m_numero As Integer
        Public Property cliente() As String
            Get
                Return m_cliente
            End Get
            Set
                m_cliente = Value
            End Set
        End Property
        Private m_cliente As String
        Public Property contacto() As String
            Get
                Return m_contacto
            End Get
            Set
                m_contacto = Value
            End Set
        End Property
        Private m_contacto As String
        Public Property fecha() As Nullable(Of System.DateTime)
            Get
                Return m_fecha
            End Get
            Set
                m_fecha = Value
            End Set
        End Property
        Private m_fecha As Nullable(Of System.DateTime)
        Public Property formaPago() As String
            Get
                Return m_formaPago
            End Get
            Set
                m_formaPago = Value
            End Set
        End Property
        Private m_formaPago As String
        Public Property plazosPago() As String
            Get
                Return m_plazosPago
            End Get
            Set
                m_plazosPago = Value
            End Set
        End Property
        Private m_plazosPago As String
        Public Property primerVencimiento() As Nullable(Of System.DateTime)
            Get
                Return m_primerVencimiento
            End Get
            Set
                m_primerVencimiento = Value
            End Set
        End Property
        Private m_primerVencimiento As Nullable(Of System.DateTime)
        Public Property iva() As String
            Get
                Return m_iva
            End Get
            Set
                m_iva = Value
            End Set
        End Property
        Private m_iva As String
        Public Property vendedor() As String
            Get
                Return m_vendedor
            End Get
            Set
                m_vendedor = Value
            End Set
        End Property
        Private m_vendedor As String
        Public Property comentarios() As String
            Get
                Return m_comentarios
            End Get
            Set
                m_comentarios = Value
            End Set
        End Property
        Private m_comentarios As String
        Public Property comentarioPicking() As String
            Get
                Return m_comentarioPicking
            End Get
            Set
                m_comentarioPicking = Value
            End Set
        End Property
        Private m_comentarioPicking As String
        Public Property periodoFacturacion() As String
            Get
                Return m_periodoFacturacion
            End Get
            Set
                m_periodoFacturacion = Value
            End Set
        End Property
        Private m_periodoFacturacion As String
        Public Property ruta() As String
            Get
                Return m_ruta
            End Get
            Set
                m_ruta = Value
            End Set
        End Property
        Private m_ruta As String
        Public Property serie() As String
            Get
                Return m_serie
            End Get
            Set
                m_serie = Value
            End Set
        End Property
        Private m_serie As String
        Public Property ccc() As String
            Get
                Return m_ccc
            End Get
            Set
                m_ccc = Value
            End Set
        End Property
        Private m_ccc As String
        Public Property origen() As String
            Get
                Return m_origen
            End Get
            Set
                m_origen = Value
            End Set
        End Property
        Private m_origen As String
        Public Property contactoCobro() As String
            Get
                Return m_contactoCobro
            End Get
            Set
                m_contactoCobro = Value
            End Set
        End Property
        Private m_contactoCobro As String
        Public Property noComisiona() As Decimal
            Get
                Return m_noComisiona
            End Get
            Set
                m_noComisiona = Value
            End Set
        End Property
        Private m_noComisiona As Decimal
        Public Property vistoBuenoPlazosPago() As Boolean

        Public Property mantenerJunto() As Boolean

        Public Property servirJunto() As Boolean

        Public Property usuario() As String

        Public Overridable Property LineasPedido() As ICollection(Of LineaPedidoVentaDTO)

    End Class
End Class
