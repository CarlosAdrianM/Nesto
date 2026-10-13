Imports System.Globalization
Imports Nesto.Infrastructure.Shared

''' <summary>
''' NestoAPI#593 (estadística): GET api/ChequesRegalo/Resumen. Una fila por vendedor (el de estética de la ficha del
''' cliente) con lo generado, usado, pendiente y caducado; la API ya filtra por lo que puede ver el usuario (Dirección
''' todos, jefe de ventas su equipo, el resto el suyo).
''' </summary>
Public Class ResumenChequesRegalo
    Public Property Campana As CampanaChequesRegaloResumen
    Public Property Vendedores As List(Of ResumenChequesVendedor)
    Public Property Total As ResumenChequesVendedor

    ''' <summary>Las filas de los vendedores y, si hay más de una, el total al final.</summary>
    Public ReadOnly Property FilasConTotal As List(Of ResumenChequesVendedor)
        Get
            Dim filas As New List(Of ResumenChequesVendedor)(If(Vendedores, New List(Of ResumenChequesVendedor)))
            If filas.Count > 1 AndAlso Total IsNot Nothing Then
                Total.EsTotal = True
                filas.Add(Total)
            End If
            Return filas
        End Get
    End Property
End Class

Public Class CampanaChequesRegaloResumen
    Private Shared ReadOnly es As CultureInfo = CultureInfo.GetCultureInfo("es-ES")

    Public Property Codigo As String
    Public Property Descripcion As String
    Public Property Importe As Decimal
    Public Property GeneraDesde As Date
    Public Property GeneraHasta As Date
    Public Property CanjeHasta As Date
    ''' <summary>Días que faltan para el último día de canje (0 = hoy es el último día).</summary>
    Public Property DiasRestantes As Integer
    Public Property Activa As Boolean
    Public Property InicioImpulsoLlamadas As Date
    Public Property EnImpulsoLlamadas As Boolean

    Public ReadOnly Property Texto As String
        Get
            Dim plazo As String = If(DiasRestantes > 0,
                $"quedan {DiasRestantes} {If(DiasRestantes = 1, "día", "días")}",
                "hoy es el último día")
            Return $"Cheque de {Importe.ToString("N0", es)} € · se puede usar hasta el {CanjeHasta.ToString("dd/MM", es)} ({plazo})"
        End Get
    End Property
End Class

Public Class ResumenChequesVendedor
    Public Property Vendedor As String
    Public Property Nombre As String
    Public Property Generados As Integer
    Public Property Usados As Integer
    Public Property EnPedido As Integer
    Public Property Servidos As Integer
    Public Property Pendientes As Integer
    Public Property PendientesDeActivar As Integer
    Public Property Caducados As Integer
    Public Property Anulados As Integer
    ''' <summary>La fila del total (en negrita, no selecciona vendedor).</summary>
    Public Property EsTotal As Boolean
End Class

''' <summary>NestoAPI#593: GET api/ChequesRegalo/Pendientes, un cheque sin usar con lo necesario para llamar al cliente.</summary>
Public Class ChequeRegaloPendiente
    Private Shared ReadOnly es As CultureInfo = CultureInfo.GetCultureInfo("es-ES")

    Public Property Id As Integer
    Public Property Campana As String
    Public Property Empresa As String
    Public Property Cliente As String
    Public Property Contacto As String
    Public Property Nombre As String
    Public Property PersonaContacto As String
    Public Property Direccion As String
    Public Property CodigoPostal As String
    Public Property Poblacion As String
    Public Property Provincia As String
    Public Property Telefono As String
    Public Property Vendedor As String
    Public Property NombreVendedor As String
    ''' <summary>Disponible o PendienteDeActivar (o el resto de estados con todos=true).</summary>
    Public Property Estado As String
    Public Property Importe As Decimal
    Public Property FechaActivacion As Date?
    Public Property CanjeHasta As Date
    Public Property DiasRestantes As Integer
    Public Property EmpresaFactura As String
    Public Property FacturaOrigen As String
    Public Property FechaFactura As Date?
    Public Property BaseImponibleFactura As Decimal?
    Public Property TotalFactura As Decimal?
    Public Property PedidoCanje As Integer?
    Public Property FechaUltimoContacto As Date?
    Public Property DiasDesdeUltimoContacto As Integer?
    Public Property FechaUltimoIntento As Date?

    Public ReadOnly Property TextoCliente As String
        Get
            Return $"{Cliente?.Trim()}/{Contacto?.Trim()} · {Nombre?.Trim()}"
        End Get
    End Property

    Public ReadOnly Property TextoDireccion As String
        Get
            Dim partes As New List(Of String)
            If Not String.IsNullOrWhiteSpace(PersonaContacto) Then partes.Add(PersonaContacto.Trim())
            If Not String.IsNullOrWhiteSpace(Direccion) Then partes.Add(Direccion.Trim())
            Dim lugar As String = $"{CodigoPostal?.Trim()} {Poblacion?.Trim()}".Trim()
            If lugar <> String.Empty Then partes.Add(lugar)
            Return String.Join(" · ", partes)
        End Get
    End Property

    Public ReadOnly Property TextoDiasRestantes As String
        Get
            If Estado = "PendienteDeActivar" Then
                Return If(FechaActivacion.HasValue, $"Se activa el {FechaActivacion.Value.ToString("dd/MM", es)}", "Aún sin activar")
            End If
            If DiasRestantes <= 0 Then
                Return "Último día"
            End If
            Return If(DiasRestantes = 1, "Queda 1 día", $"Quedan {DiasRestantes} días")
        End Get
    End Property

    Public ReadOnly Property TextoFactura As String
        Get
            If String.IsNullOrWhiteSpace(FacturaOrigen) Then
                Return String.Empty
            End If
            Dim texto As String = $"Factura {FacturaOrigen.Trim()}"
            If FechaFactura.HasValue Then
                texto &= $" del {FechaFactura.Value.ToString("dd/MM", es)}"
            End If
            If TotalFactura.HasValue Then
                texto &= $": {TotalFactura.Value.ToString("N2", es)} €"
            End If
            Return texto
        End Get
    End Property

    Public ReadOnly Property TextoUltimoContacto As String
        Get
            If Not DiasDesdeUltimoContacto.HasValue Then
                Return "Sin contacto reciente"
            End If
            Select Case DiasDesdeUltimoContacto.Value
                Case 0
                    Return "Hablasteis hoy"
                Case 1
                    Return "Hablasteis ayer"
                Case Else
                    Return $"Hablasteis hace {DiasDesdeUltimoContacto.Value} días"
            End Select
        End Get
    End Property

    Public ReadOnly Property TieneFactura As Boolean
        Get
            Return Not String.IsNullOrWhiteSpace(FacturaOrigen)
        End Get
    End Property

    ''' <summary>El primer móvil de la ficha (para WhatsApp), o Nothing.</summary>
    Public ReadOnly Property Movil As String
        Get
            Return TelefonoHelper.PrimerMovil(Telefono)
        End Get
    End Property

    Public ReadOnly Property TieneMovil As Boolean
        Get
            Return Movil IsNot Nothing
        End Get
    End Property
End Class
