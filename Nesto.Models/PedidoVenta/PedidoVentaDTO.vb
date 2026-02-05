Imports System.Collections.ObjectModel
Imports System.Runtime.Serialization


''' <summary>
''' DTO para pedidos de venta. Implementa IEquatable para detectar cambios sin guardar.
''' </summary>
Public Class PedidoVentaDTO
    Inherits PedidoBase(Of LineaPedidoVentaDTO)
    Implements IEquatable(Of PedidoVentaDTO)

    Public Sub New()
        Lineas = New List(Of LineaPedidoVentaDTO)()
        Prepagos = New ObservableCollection(Of PrepagoDTO)
        Efectos = New ListaEfectos()
    End Sub

    Public Property empresa() As String
    Public Property numero() As Integer
    Public Property cliente() As String
    Public Property contacto() As String
    Public Property fecha() As Nullable(Of System.DateTime)
    Public Property formaPago() As String
    Public Property plazosPago() As String
    Public Property primerVencimiento() As Nullable(Of System.DateTime)
    Public Property iva() As String
    Public Property vendedor As String
    Public Property comentarios() As String
    Public Property comentarioPicking() As String
    Public Property CrearEfectosManualmente As Boolean
    Public Property periodoFacturacion() As String
    Public Property ruta() As String
    Private _serie As String
    ''' <summary>
    ''' Serie del pedido (NV, CV, etc.). Se normaliza con Trim() para evitar problemas con valores de BD.
    ''' Carlos 09/12/25: Fix para evitar que "CV " no coincida con "CV" en selectores.
    ''' </summary>
    Public Property serie() As String
        Get
            Return _serie
        End Get
        Set(value As String)
            _serie = value?.Trim()
        End Set
    End Property
    Private _ccc As String
    Public Property ccc() As String
        Get
            Return _ccc
        End Get
        Set(value As String)
            If _ccc <> value Then
                _ccc = value
                Efectos.CccCliente = value
            End If
        End Set
    End Property
    Public Property origen() As String
    Public Property contactoCobro() As String
    Public Property noComisiona() As Decimal
    Public Property vistoBuenoPlazosPago() As Boolean
    Public Property mantenerJunto() As Boolean
    Public Property servirJunto() As Boolean
    Public Property EsPresupuesto() As Boolean = False
    Public Property notaEntrega As Boolean
    Public Property CreadoSinPasarValidacion As Boolean

    Public ReadOnly Property Bruto As Decimal
        Get
            Return Lineas.Sum(Function(l) l.Bruto)
        End Get
    End Property
    Public ReadOnly Property baseImponiblePicking As Decimal
        Get
            Dim baseImponibleParcial As Decimal
            For Each linea In Lineas.Where(Function(l) l.picking > 0)
                baseImponibleParcial += linea.BaseImponible
            Next
            Return baseImponibleParcial
        End Get
    End Property

    Public ReadOnly Property brutoPicking As Decimal
        Get
            Return Lineas.Where(Function(l) l.picking > 0).Sum(Function(l) l.Bruto)
        End Get
    End Property

    Public ReadOnly Property sumaDescuentos(linea As LineaPedidoVentaDTO) As Decimal
        Get
            ' Si no está marcado el aplicar descuento, solo aplica el de la propia línea
            If Not linea.aplicarDescuento Then
                Return linea.DescuentoLinea
            End If

            ' falta añadir el descuento del cliente y el descuento PP
            Return 1 - ((1 - linea.DescuentoLinea) * (1 - linea.DescuentoProducto))
        End Get
    End Property

    Public ReadOnly Property totalPicking As Decimal
        Get
            Dim totalParcial As Decimal
            For Each linea In Lineas.Where(Function(l) l.picking > 0)
                totalParcial += linea.Total
            Next
            Return totalParcial
        End Get
    End Property

    Public Overridable Property Efectos As ListaEfectos
    Public Overridable Property Prepagos() As ObservableCollection(Of PrepagoDTO)
    Public Overridable Property VendedoresGrupoProducto As ObservableCollection(Of VendedorGrupoProductoDTO)

    <OnDeserialized>
    Private Sub OnDeserializedMethod(context As StreamingContext)
        ' Establecer la referencia Pedido en cada línea después de la deserialización
        For Each linea As LineaPedidoVentaDTO In Lineas
            linea.Pedido = Me
        Next
    End Sub

#Region "IEquatable - Comparación para detectar cambios sin guardar (Issue #254)"

    ''' <summary>
    ''' Compara los campos editables del pedido que afectan a la facturación.
    ''' No compara las líneas (solo cabecera) ni campos de solo lectura.
    ''' </summary>
    Public Overloads Function Equals(other As PedidoVentaDTO) As Boolean Implements IEquatable(Of PedidoVentaDTO).Equals
        If other Is Nothing Then Return False
        If ReferenceEquals(Me, other) Then Return True

        ' Comparamos solo los campos que el usuario puede modificar y afectan a facturación
        ' Log temporal para diagnóstico (Issue #254)
        If Not StringsIguales(formaPago, other.formaPago) Then Debug.WriteLine($"[PedidoVentaDTO.Equals] Diferencia en formaPago: '{formaPago}' vs '{other.formaPago}'")
        If Not StringsIguales(plazosPago, other.plazosPago) Then Debug.WriteLine($"[PedidoVentaDTO.Equals] Diferencia en plazosPago: '{plazosPago}' vs '{other.plazosPago}'")
        If Not StringsIguales(ccc, other.ccc) Then Debug.WriteLine($"[PedidoVentaDTO.Equals] Diferencia en ccc: '{ccc}' vs '{other.ccc}'")
        If Not StringsIguales(iva, other.iva) Then Debug.WriteLine($"[PedidoVentaDTO.Equals] Diferencia en iva: '{iva}' vs '{other.iva}'")
        If Not StringsIguales(vendedor, other.vendedor) Then Debug.WriteLine($"[PedidoVentaDTO.Equals] Diferencia en vendedor: '{vendedor}' vs '{other.vendedor}'")
        If Not StringsIguales(periodoFacturacion, other.periodoFacturacion) Then Debug.WriteLine($"[PedidoVentaDTO.Equals] Diferencia en periodoFacturacion: '{periodoFacturacion}' vs '{other.periodoFacturacion}'")
        If Not StringsIguales(ruta, other.ruta) Then Debug.WriteLine($"[PedidoVentaDTO.Equals] Diferencia en ruta: '{ruta}' vs '{other.ruta}'")
        If Not StringsIguales(serie, other.serie) Then Debug.WriteLine($"[PedidoVentaDTO.Equals] Diferencia en serie: '{serie}' vs '{other.serie}'")
        If Not StringsIguales(contacto, other.contacto) Then Debug.WriteLine($"[PedidoVentaDTO.Equals] Diferencia en contacto: '{contacto}' vs '{other.contacto}'")
        If Not StringsIguales(contactoCobro, other.contactoCobro) Then Debug.WriteLine($"[PedidoVentaDTO.Equals] Diferencia en contactoCobro: '{contactoCobro}' vs '{other.contactoCobro}'")
        If Not StringsIguales(comentarios, other.comentarios) Then Debug.WriteLine($"[PedidoVentaDTO.Equals] Diferencia en comentarios: '{comentarios}' vs '{other.comentarios}'")
        If Not StringsIguales(comentarioPicking, other.comentarioPicking) Then Debug.WriteLine($"[PedidoVentaDTO.Equals] Diferencia en comentarioPicking: '{comentarioPicking}' vs '{other.comentarioPicking}'")
        If Not Nullable.Equals(primerVencimiento, other.primerVencimiento) Then Debug.WriteLine($"[PedidoVentaDTO.Equals] Diferencia en primerVencimiento: '{primerVencimiento}' vs '{other.primerVencimiento}'")
        If noComisiona <> other.noComisiona Then Debug.WriteLine($"[PedidoVentaDTO.Equals] Diferencia en noComisiona: '{noComisiona}' vs '{other.noComisiona}'")
        If mantenerJunto <> other.mantenerJunto Then Debug.WriteLine($"[PedidoVentaDTO.Equals] Diferencia en mantenerJunto: '{mantenerJunto}' vs '{other.mantenerJunto}'")
        If servirJunto <> other.servirJunto Then Debug.WriteLine($"[PedidoVentaDTO.Equals] Diferencia en servirJunto: '{servirJunto}' vs '{other.servirJunto}'")
        If notaEntrega <> other.notaEntrega Then Debug.WriteLine($"[PedidoVentaDTO.Equals] Diferencia en notaEntrega: '{notaEntrega}' vs '{other.notaEntrega}'")

        Return StringsIguales(formaPago, other.formaPago) AndAlso
               StringsIguales(plazosPago, other.plazosPago) AndAlso
               StringsIguales(ccc, other.ccc) AndAlso
               StringsIguales(iva, other.iva) AndAlso
               StringsIguales(vendedor, other.vendedor) AndAlso
               StringsIguales(periodoFacturacion, other.periodoFacturacion) AndAlso
               StringsIguales(ruta, other.ruta) AndAlso
               StringsIguales(serie, other.serie) AndAlso
               StringsIguales(contacto, other.contacto) AndAlso
               StringsIguales(contactoCobro, other.contactoCobro) AndAlso
               StringsIguales(comentarios, other.comentarios) AndAlso
               StringsIguales(comentarioPicking, other.comentarioPicking) AndAlso
               Nullable.Equals(primerVencimiento, other.primerVencimiento) AndAlso
               noComisiona = other.noComisiona AndAlso
               mantenerJunto = other.mantenerJunto AndAlso
               servirJunto = other.servirJunto AndAlso
               notaEntrega = other.notaEntrega
    End Function

    ''' <summary>
    ''' Compara dos strings tratando Nothing y "" como iguales, y aplicando Trim().
    ''' Esto es necesario porque:
    ''' - La API puede devolver Nothing y el DTO puede tener ""
    ''' - La BD tiene campos de longitud fija que vienen con espacios al final
    ''' </summary>
    Private Shared Function StringsIguales(s1 As String, s2 As String) As Boolean
        ' Normalizar: tratar Nothing como "" y quitar espacios
        Dim str1 = If(s1, String.Empty).Trim()
        Dim str2 = If(s2, String.Empty).Trim()
        Return String.Equals(str1, str2, StringComparison.Ordinal)
    End Function

    Public Overrides Function Equals(obj As Object) As Boolean
        Return Equals(TryCast(obj, PedidoVentaDTO))
    End Function

    Public Overrides Function GetHashCode() As Integer
        ' Usamos XOR para evitar overflow en las operaciones
        Dim hash As Integer = 17
        hash = hash Xor If(formaPago IsNot Nothing, formaPago.GetHashCode(), 0)
        hash = hash Xor If(ccc IsNot Nothing, ccc.GetHashCode(), 0)
        hash = hash Xor If(plazosPago IsNot Nothing, plazosPago.GetHashCode(), 0)
        hash = hash Xor If(iva IsNot Nothing, iva.GetHashCode(), 0)
        Return hash
    End Function

    ''' <summary>
    ''' Crea una copia superficial del pedido para guardar como snapshot.
    ''' Solo copia los campos comparados en Equals.
    ''' </summary>
    Public Function CrearSnapshot() As PedidoVentaDTO
        Return New PedidoVentaDTO() With {
            .empresa = Me.empresa,
            .numero = Me.numero,
            .cliente = Me.cliente,
            .contacto = Me.contacto,
            .formaPago = Me.formaPago,
            .plazosPago = Me.plazosPago,
            .primerVencimiento = Me.primerVencimiento,
            .ccc = Me.ccc,
            .iva = Me.iva,
            .vendedor = Me.vendedor,
            .periodoFacturacion = Me.periodoFacturacion,
            .ruta = Me.ruta,
            .serie = Me.serie,
            .contactoCobro = Me.contactoCobro,
            .comentarios = Me.comentarios,
            .comentarioPicking = Me.comentarioPicking,
            .noComisiona = Me.noComisiona,
            .mantenerJunto = Me.mantenerJunto,
            .servirJunto = Me.servirJunto,
            .notaEntrega = Me.notaEntrega
        }
    End Function

#End Region

#Region "Helpers para lógica de negocio"

    ''' <summary>
    ''' Determina si se deben sobrescribir los datos del cliente (origen, contactoCobro)
    ''' al asignar ClienteCompleto en el ViewModel.
    ''' Solo se sobrescriben en pedidos NUEVOS, no en existentes.
    ''' </summary>
    ''' <param name="esPedidoNuevo">True si el pedido es nuevo (numero = 0)</param>
    ''' <returns>True si se deben sobrescribir los datos del cliente</returns>
    Public Shared Function DebeSobrescribirDatosCliente(esPedidoNuevo As Boolean) As Boolean
        Return esPedidoNuevo
    End Function

#End Region

End Class

