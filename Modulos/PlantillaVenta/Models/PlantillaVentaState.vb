Imports Nesto.Models
Imports Prism.Mvvm

''' <summary>
''' Estado completo de una PlantillaVenta.
''' Issue #287: Modelo unificado para el estado de la plantilla.
'''
''' Este objeto contiene TODO el estado necesario para:
''' 1. Guardar/cargar borradores (serialización JSON)
''' 2. Convertir a PedidoVentaDTO (método ToPedidoVentaDTO)
''' 3. Compartir entre Nesto y NestoApp
''' </summary>
Public Class PlantillaVentaState
    Inherits BindableBase

#Region "Identificación del cliente"
    ''' <summary>
    ''' Empresa del cliente
    ''' </summary>
    Public Property Empresa As String

    ''' <summary>
    ''' Número de cliente
    ''' </summary>
    Public Property Cliente As String

    ''' <summary>
    ''' Contacto/dirección de entrega seleccionada
    ''' </summary>
    Public Property Contacto As String

    ''' <summary>
    ''' Nombre del cliente (para mostrar en UI y borradores)
    ''' </summary>
    Public Property NombreCliente As String
#End Region

#Region "Datos del cliente (copiados de clienteSeleccionado)"
    ''' <summary>
    ''' IVA del cliente (G21, R52, etc.)
    ''' </summary>
    Public Property IvaCliente As String

    ''' <summary>
    ''' Estado del cliente (distribuidor, etc.) - necesario para calcular serie
    ''' </summary>
    Public Property EstadoCliente As Integer

    ''' <summary>
    ''' Comentario de picking del cliente
    ''' </summary>
    Public Property ComentarioPickingCliente As String
#End Region

#Region "Datos de la dirección de entrega (copiados de direccionEntregaSeleccionada)"
    ''' <summary>
    ''' Vendedor asignado a la dirección
    ''' </summary>
    Public Property Vendedor As String

    ''' <summary>
    ''' Periodo de facturación
    ''' </summary>
    Public Property PeriodoFacturacion As String

    ''' <summary>
    ''' Ruta de entrega
    ''' </summary>
    Public Property Ruta As String

    ''' <summary>
    ''' Cuenta bancaria (CCC)
    ''' </summary>
    Public Property Ccc As String

    ''' <summary>
    ''' Si no comisiona
    ''' </summary>
    Public Property NoComisiona As Boolean

    ''' <summary>
    ''' Si mantener junto en el reparto
    ''' </summary>
    Public Property MantenerJunto As Boolean

    ''' <summary>
    ''' Si servir junto (afecta stock disponible)
    ''' </summary>
    Public Property ServirJunto As Boolean
#End Region

#Region "Líneas de productos"
    Private _lineasProducto As List(Of LineaPlantillaVenta)
    ''' <summary>
    ''' Líneas de productos del pedido.
    ''' Incluye cantidad, cantidadOferta, precio, descuento, etc.
    ''' </summary>
    Public Property LineasProducto As List(Of LineaPlantillaVenta)
        Get
            If _lineasProducto Is Nothing Then
                _lineasProducto = New List(Of LineaPlantillaVenta)
            End If
            Return _lineasProducto
        End Get
        Set(value As List(Of LineaPlantillaVenta))
            SetProperty(_lineasProducto, value)
        End Set
    End Property

    Private _lineasRegalo As List(Of LineaRegalo)
    ''' <summary>
    ''' Líneas de regalos (Ganavisiones).
    ''' </summary>
    Public Property LineasRegalo As List(Of LineaRegalo)
        Get
            If _lineasRegalo Is Nothing Then
                _lineasRegalo = New List(Of LineaRegalo)
            End If
            Return _lineasRegalo
        End Get
        Set(value As List(Of LineaRegalo))
            SetProperty(_lineasRegalo, value)
        End Set
    End Property
#End Region

#Region "Configuración de venta"
    Private _formaVenta As Integer = 1
    ''' <summary>
    ''' Forma de venta: 1=Directa, 2=Teléfono, 3+=Otras
    ''' </summary>
    Public Property FormaVenta As Integer
        Get
            Return _formaVenta
        End Get
        Set(value As Integer)
            SetProperty(_formaVenta, value)
        End Set
    End Property

    ''' <summary>
    ''' Código de forma de venta cuando FormaVenta > 2
    ''' </summary>
    Public Property FormaVentaOtrasCodigo As String

    ''' <summary>
    ''' Código de forma de pago
    ''' </summary>
    Public Property FormaPago As String

    ''' <summary>
    ''' Código de plazos de pago
    ''' </summary>
    Public Property PlazosPago As String

    ''' <summary>
    ''' Descuento por pronto pago (del plazo seleccionado)
    ''' </summary>
    Public Property DescuentoPP As Decimal

    Private _esPresupuesto As Boolean
    ''' <summary>
    ''' Si es un presupuesto en lugar de un pedido
    ''' </summary>
    Public Property EsPresupuesto As Boolean
        Get
            Return _esPresupuesto
        End Get
        Set(value As Boolean)
            SetProperty(_esPresupuesto, value)
        End Set
    End Property
#End Region

#Region "Almacén y entrega"
    ''' <summary>
    ''' Código del almacén seleccionado
    ''' </summary>
    Public Property AlmacenCodigo As String

    Private _fechaEntrega As Date = Date.Today
    ''' <summary>
    ''' Fecha de entrega solicitada
    ''' </summary>
    Public Property FechaEntrega As Date
        Get
            Return _fechaEntrega
        End Get
        Set(value As Date)
            SetProperty(_fechaEntrega, value)
        End Set
    End Property

    Private _enviarPorGlovo As Boolean
    ''' <summary>
    ''' Si se envía por Glovo (entrega urgente)
    ''' </summary>
    Public Property EnviarPorGlovo As Boolean
        Get
            Return _enviarPorGlovo
        End Get
        Set(value As Boolean)
            SetProperty(_enviarPorGlovo, value)
        End Set
    End Property
#End Region

#Region "Comentarios"
    Private _comentarioRuta As String
    ''' <summary>
    ''' Comentario para la ruta de entrega
    ''' </summary>
    Public Property ComentarioRuta As String
        Get
            Return _comentarioRuta
        End Get
        Set(value As String)
            SetProperty(_comentarioRuta, value)
        End Set
    End Property

    Private _comentarioPicking As String
    ''' <summary>
    ''' Comentario de picking (introducido por el usuario, adicional al del cliente)
    ''' </summary>
    Public Property ComentarioPicking As String
        Get
            Return _comentarioPicking
        End Get
        Set(value As String)
            SetProperty(_comentarioPicking, value)
        End Set
    End Property
#End Region

#Region "Cobro por tarjeta"
    Private _mandarCobroTarjeta As Boolean
    ''' <summary>
    ''' Si se debe enviar solicitud de cobro por tarjeta
    ''' </summary>
    Public Property MandarCobroTarjeta As Boolean
        Get
            Return _mandarCobroTarjeta
        End Get
        Set(value As Boolean)
            SetProperty(_mandarCobroTarjeta, value)
        End Set
    End Property

    Private _cobroTarjetaCorreo As String
    ''' <summary>
    ''' Email para enviar el cobro por tarjeta
    ''' </summary>
    Public Property CobroTarjetaCorreo As String
        Get
            Return _cobroTarjetaCorreo
        End Get
        Set(value As String)
            SetProperty(_cobroTarjetaCorreo, value)
        End Set
    End Property

    Private _cobroTarjetaMovil As String
    ''' <summary>
    ''' Teléfono móvil para enviar el cobro por tarjeta
    ''' </summary>
    Public Property CobroTarjetaMovil As String
        Get
            Return _cobroTarjetaMovil
        End Get
        Set(value As String)
            SetProperty(_cobroTarjetaMovil, value)
        End Set
    End Property
#End Region

#Region "Metadatos del borrador"
    ''' <summary>
    ''' ID del borrador (GUID) - solo se usa cuando se guarda/carga como borrador
    ''' </summary>
    Public Property BorradorId As String

    ''' <summary>
    ''' Fecha de creación del borrador
    ''' </summary>
    Public Property BorradorFechaCreacion As DateTime

    ''' <summary>
    ''' Usuario que creó el borrador
    ''' </summary>
    Public Property BorradorUsuario As String

    ''' <summary>
    ''' Mensaje de error que causó la creación del borrador (si aplica)
    ''' </summary>
    Public Property BorradorMensajeError As String
#End Region

#Region "Propiedades calculadas"
    ''' <summary>
    ''' Número de líneas de productos con cantidad
    ''' </summary>
    Public ReadOnly Property NumeroLineasProducto As Integer
        Get
            If LineasProducto Is Nothing Then Return 0
            Return LineasProducto.Where(Function(l) l.cantidad > 0 OrElse l.cantidadOferta > 0).Count()
        End Get
    End Property

    ''' <summary>
    ''' Número de líneas de regalo con cantidad
    ''' </summary>
    Public ReadOnly Property NumeroLineasRegalo As Integer
        Get
            If LineasRegalo Is Nothing Then Return 0
            Return LineasRegalo.Where(Function(l) l.cantidad > 0).Count()
        End Get
    End Property

    ''' <summary>
    ''' Total de líneas (productos + regalos)
    ''' </summary>
    Public ReadOnly Property NumeroLineasTotal As Integer
        Get
            Return NumeroLineasProducto + NumeroLineasRegalo
        End Get
    End Property

    ''' <summary>
    ''' Base imponible del pedido (sin regalos)
    ''' </summary>
    Public ReadOnly Property BaseImponible As Decimal
        Get
            If LineasProducto Is Nothing Then Return 0
            Return LineasProducto.Where(Function(l) l.cantidad > 0).Sum(Function(l) l.baseImponible)
        End Get
    End Property

    ''' <summary>
    ''' Descripción corta para mostrar en lista de borradores
    ''' </summary>
    Public ReadOnly Property DescripcionBorrador As String
        Get
            Dim lineasInfo = $"{NumeroLineasTotal} líneas"
            If NumeroLineasRegalo > 0 Then
                lineasInfo &= $" ({NumeroLineasRegalo} regalos)"
            End If
            Return $"{Cliente} - {NombreCliente} ({lineasInfo}) - {BorradorFechaCreacion:dd/MM/yyyy HH:mm}"
        End Get
    End Property
#End Region

#Region "Métodos"
    ''' <summary>
    ''' Crea una copia profunda del estado.
    ''' </summary>
    Public Function Clonar() As PlantillaVentaState
        ' Serializar y deserializar para copia profunda
        Dim json = Newtonsoft.Json.JsonConvert.SerializeObject(Me)
        Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of PlantillaVentaState)(json)
    End Function

    ''' <summary>
    ''' Limpia el estado para comenzar una nueva plantilla.
    ''' </summary>
    Public Sub Limpiar()
        Empresa = Nothing
        Cliente = Nothing
        Contacto = Nothing
        NombreCliente = Nothing
        IvaCliente = Nothing
        EstadoCliente = 0
        ComentarioPickingCliente = Nothing
        Vendedor = Nothing
        PeriodoFacturacion = Nothing
        Ruta = Nothing
        Ccc = Nothing
        NoComisiona = False
        MantenerJunto = False
        ServirJunto = False
        LineasProducto = New List(Of LineaPlantillaVenta)
        LineasRegalo = New List(Of LineaRegalo)
        FormaVenta = 1
        FormaVentaOtrasCodigo = Nothing
        FormaPago = Nothing
        PlazosPago = Nothing
        DescuentoPP = 0
        EsPresupuesto = False
        AlmacenCodigo = Nothing
        FechaEntrega = Date.Today
        EnviarPorGlovo = False
        ComentarioRuta = Nothing
        ComentarioPicking = Nothing
        MandarCobroTarjeta = False
        CobroTarjetaCorreo = Nothing
        CobroTarjetaMovil = Nothing
        BorradorId = Nothing
        BorradorFechaCreacion = DateTime.MinValue
        BorradorUsuario = Nothing
        BorradorMensajeError = Nothing
    End Sub

    ''' <summary>
    ''' Convierte el estado a un PedidoVentaDTO listo para enviar a la API.
    ''' El ViewModel debe completar los campos que dependen del contexto
    ''' (usuario, delegación, etc.) después de llamar a este método.
    ''' </summary>
    ''' <param name="formaVentaCodigo">Código de forma de venta (DIR, TEL, o el código de otras)</param>
    ''' <param name="ultimaOfertaFunc">Función para obtener el siguiente número de oferta</param>
    ''' <param name="calcularSerieFunc">Función para calcular la serie del pedido</param>
    Public Function ToPedidoVentaDTO(
        formaVentaCodigo As String,
        ultimaOfertaFunc As Func(Of Integer),
        calcularSerieFunc As Func(Of String)
    ) As PedidoVentaDTO

        Const ESTADO_LINEA_CURSO As Integer = 1
        Const ESTADO_LINEA_PRESUPUESTO As Integer = -3

        Dim pedido As New PedidoVentaDTO With {
            .empresa = Empresa,
            .cliente = Cliente,
            .contacto = Contacto,
            .EsPresupuesto = EsPresupuesto,
            .fecha = Date.Today,
            .formaPago = If(String.IsNullOrEmpty(FormaPago), String.Empty, FormaPago),
            .plazosPago = If(String.IsNullOrEmpty(PlazosPago), String.Empty, PlazosPago),
            .DescuentoPP = DescuentoPP,
            .primerVencimiento = Date.Today,
            .iva = IvaCliente,
            .vendedor = Vendedor,
            .periodoFacturacion = PeriodoFacturacion,
            .ruta = If(EnviarPorGlovo, "GLV", Ruta),
            .serie = calcularSerieFunc(),
            .ccc = Ccc,
            .origen = Empresa,
            .contactoCobro = Contacto,
            .noComisiona = NoComisiona,
            .mantenerJunto = MantenerJunto,
            .servirJunto = ServirJunto,
            .comentarioPicking = ComentarioPickingCliente,
            .comentarios = ComentarioRuta
        }

        ' Añadir líneas de productos
        For Each linea In LineasProducto.Where(Function(l) l.cantidad > 0 OrElse l.cantidadOferta > 0)
            Dim ofertaLinea As Integer? = If(linea.cantidadOferta <> 0, ultimaOfertaFunc(), DirectCast(Nothing, Integer?))

            ' Línea principal (cantidad de pago)
            If linea.cantidad > 0 Then
                Dim lineaPedido As New LineaPedidoVentaDTO With {
                    .Pedido = pedido,
                    .estado = If(EsPresupuesto, ESTADO_LINEA_PRESUPUESTO, ESTADO_LINEA_CURSO),
                    .tipoLinea = 1,
                    .Producto = linea.producto,
                    .texto = linea.texto,
                    .Cantidad = linea.cantidad,
                    .fechaEntrega = FechaEntrega,
                    .PrecioUnitario = linea.precio,
                    .DescuentoLinea = If(linea.descuento = linea.descuentoProducto, 0, linea.descuento),
                    .DescuentoProducto = If(linea.descuento = linea.descuentoProducto, linea.descuentoProducto, 0),
                    .AplicarDescuento = If(linea.descuento = linea.descuentoProducto, linea.aplicarDescuento, False),
                    .vistoBueno = 0,
                    .almacen = AlmacenCodigo,
                    .iva = linea.iva,
                    .formaVenta = formaVentaCodigo,
                    .oferta = ofertaLinea
                }
                pedido.Lineas.Add(lineaPedido)
            End If

            ' Línea de oferta (cantidad gratis)
            If linea.cantidadOferta > 0 Then
                Dim lineaPedidoOferta As New LineaPedidoVentaDTO With {
                    .Pedido = pedido,
                    .estado = If(EsPresupuesto, ESTADO_LINEA_PRESUPUESTO, ESTADO_LINEA_CURSO),
                    .tipoLinea = 1,
                    .Producto = linea.producto,
                    .texto = linea.texto,
                    .Cantidad = linea.cantidadOferta,
                    .fechaEntrega = FechaEntrega,
                    .PrecioUnitario = 0,
                    .DescuentoLinea = 0,
                    .DescuentoProducto = 0,
                    .AplicarDescuento = False,
                    .vistoBueno = 0,
                    .almacen = AlmacenCodigo,
                    .iva = linea.iva,
                    .formaVenta = formaVentaCodigo,
                    .oferta = ofertaLinea
                }
                pedido.Lineas.Add(lineaPedidoOferta)
            End If
        Next

        ' Añadir líneas de regalos (Ganavisiones)
        For Each lineaRegalo In LineasRegalo.Where(Function(r) r.cantidad > 0)
            Dim textoBonificado = lineaRegalo.texto & " (BONIFICADO)"
            If textoBonificado.Length > 50 Then
                textoBonificado = textoBonificado.Substring(0, 50)
            End If

            Dim lineaPedidoRegalo As New LineaPedidoVentaDTO With {
                .Pedido = pedido,
                .estado = If(EsPresupuesto, ESTADO_LINEA_PRESUPUESTO, ESTADO_LINEA_CURSO),
                .tipoLinea = 1,
                .Producto = lineaRegalo.producto,
                .texto = textoBonificado,
                .Cantidad = lineaRegalo.cantidad,
                .fechaEntrega = FechaEntrega,
                .PrecioUnitario = lineaRegalo.precio,
                .DescuentoLinea = 1,
                .DescuentoProducto = 0,
                .AplicarDescuento = False,
                .vistoBueno = 0,
                .almacen = AlmacenCodigo,
                .iva = lineaRegalo.iva,
                .formaVenta = formaVentaCodigo,
                .oferta = Nothing
            }
            pedido.Lineas.Add(lineaPedidoRegalo)
        Next

        Return pedido
    End Function
#End Region

End Class
