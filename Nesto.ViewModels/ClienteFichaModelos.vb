Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.Globalization
Imports System.IO
Imports System.Net.Http
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Windows
Imports System.Windows.Data
Imports System.Windows.Input
Imports ControlesUsuario.Dialogs
Imports ControlesUsuario.Models
Imports Microsoft.Win32
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Models
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models
Imports Nesto.Models.Nesto.Models
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.Modulos.PedidoVenta.PedidoVentaModel
Imports Nesto.Modulos.Rapports
Imports Newtonsoft.Json
Imports CommunityToolkit.Mvvm.Input
Imports CommunityToolkit.Mvvm.ComponentModel

Public Interface IOService
    Function OpenFileDialog(defaultPath As String) As String

    'Other similar untestable IO operations
    Function OpenFile(path As String) As Stream
End Interface

Public Class StringTrimmingConverter
    Implements IValueConverter

    Public Function ConvertBack(ByVal value As Object, ByVal targetType As Type, ByVal parameter As Object, ByVal culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Return value
    End Function

    Public Function Convert(ByVal value As Object, ByVal targetType As Type, ByVal parameter As Object, ByVal culture As CultureInfo) As Object Implements IValueConverter.Convert
        If IsNothing(value) Then
            Return ""
        Else
            Return value.ToString().Trim
        End If

    End Function

End Class

Public Class datosBancoConverter
    Implements IValueConverter

    Public Function ConvertBack(ByVal value As Object, ByVal targetType As Type, ByVal parameter As Object, ByVal culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Return value
    End Function

    Public Function Convert(ByVal value As Object, ByVal targetType As Type, ByVal parameter As Object, ByVal culture As CultureInfo) As Object Implements IValueConverter.Convert
        If IsNothing(value) Then
            Return "Sin datos de banco"
        Else
            Return "Tiene datos de banco"
        End If
    End Function

End Class

''' <summary>
''' Nesto#340 (1C.8, slice 3): fila del grid de seguimientos de la ficha de cliente. Los nombres
''' coinciden con SeguimientoClienteDTO de la API para deserializar sin mapeos.
''' </summary>
Public Class SeguimientoResumen
    Public Property Fecha As Date
    Public Property Tipo As String
    Public Property Vendedor As String
    Public Property Comentarios As String
    Public Property Usuario As String
End Class

Public Class lineaVentaAgrupada
    Public Property producto As String

    Public Property nombre As String
    Public Property cantidad As Integer

    Public Property familia As String

    Public Property fechaUltVenta As Date

    Public Property subGrupo As String
End Class

Public Class cabeceraPedidoAgrupada
    Public Property Numero As Integer
    ' Nesto#340 (1C.8, slice 5): nullable porque CabPedidoVta.Fecha puede venir nula de la API
    Public Property Fecha As Date?
    Public Property CCC As String
End Class

' Nesto#340 (1C.8, slice 5): POCO del CCC de la ficha de clientes. Mantiene los nombres de
' propiedad que bindea Clientes.xaml (SelectedItem.Pais, .Nº_Cuenta, etc.) y mapea con
' JsonProperty los que no coinciden con el CCCDTO de la API. El dirty que antes llevaba el
' ChangeTracker de EF lo lleva EsModificado (los setters editables lo activan).
Public Class CCCModel
    Inherits ObservableObject

    <JsonIgnore>
    Public Property EsModificado As Boolean

    ' Carlos 17/11/26: creada con «Nuevo Mandato» y aún sin guardar (no existe en la BD). No se puede usar para los
    ' recibos hasta guardarla.
    <JsonIgnore>
    Public Property EsNueva As Boolean

    Public Property Empresa As String
    Public Property Cliente As String
    Public Property Contacto As String
    <JsonProperty("numero")>
    Public Property Número As String

    Private _pais As String
    Public Property Pais As String
        Get
            Return _pais
        End Get
        Set(value As String)
            If SetProperty(_pais, value) Then
                EsModificado = True
            End If
        End Set
    End Property

    Private _dcIban As String
    <JsonProperty("dcIban")>
    Public Property DC_IBAN As String
        Get
            Return _dcIban
        End Get
        Set(value As String)
            If SetProperty(_dcIban, value) Then
                EsModificado = True
            End If
        End Set
    End Property

    Private _entidad As String
    Public Property Entidad As String
        Get
            Return _entidad
        End Get
        Set(value As String)
            If SetProperty(_entidad, value) Then
                EsModificado = True
            End If
        End Set
    End Property

    Private _oficina As String
    Public Property Oficina As String
        Get
            Return _oficina
        End Get
        Set(value As String)
            If SetProperty(_oficina, value) Then
                EsModificado = True
            End If
        End Set
    End Property

    Private _dc As String
    Public Property DC As String
        Get
            Return _dc
        End Get
        Set(value As String)
            If SetProperty(_dc, value) Then
                EsModificado = True
            End If
        End Set
    End Property

    Private _numeroCuenta As String
    <JsonProperty("numeroCuenta")>
    Public Property Nº_Cuenta As String
        Get
            Return _numeroCuenta
        End Get
        Set(value As String)
            If SetProperty(_numeroCuenta, value) Then
                EsModificado = True
            End If
        End Set
    End Property

    Private _bic As String
    Public Property BIC As String
        Get
            Return _bic
        End Get
        Set(value As String)
            If SetProperty(_bic, value) Then
                EsModificado = True
            End If
        End Set
    End Property

    Private _estado As Short
    Public Property Estado As Short
        Get
            Return _estado
        End Get
        Set(value As Short)
            If SetProperty(_estado, value) Then
                EsModificado = True
            End If
        End Set
    End Property

    Private _tipoMandato As Short?
    Public Property TipoMandato As Short?
        Get
            Return _tipoMandato
        End Get
        Set(value As Short?)
            If SetProperty(_tipoMandato, value) Then
                EsModificado = True
            End If
        End Set
    End Property

    Private _fechaMandato As Date?
    Public Property FechaMandato As Date?
        Get
            Return _fechaMandato
        End Get
        Set(value As Date?)
            If SetProperty(_fechaMandato, value) Then
                EsModificado = True
            End If
        End Set
    End Property

    Private _secuencia As String
    Public Property Secuencia As String
        Get
            Return _secuencia
        End Get
        Set(value As String)
            If SetProperty(_secuencia, value) Then
                EsModificado = True
            End If
        End Set
    End Property
End Class

' Nesto#340 (1C.8, slice 5): estados de CCC de la API; Número/Descripción son los nombres
' que bindea el combo de la vista.
Public Class EstadoCCCModel
    <JsonProperty("numero")>
    Public Property Número As Short
    <JsonProperty("descripcion")>
    Public Property Descripción As String
End Class

' Nesto#340 (1C.8, slice 5): fila del aviso de efectos pendientes con otro CCC (PUT Clientes/CCCs).
' Nombres = columnas que bindea el grid extractoCCC de Clientes.xaml.
Public Class ExtractoCCCModel
    <JsonProperty("fechaVencimiento")>
    Public Property FechaVto As Date?
    Public Property Concepto As String
    <JsonProperty("importePendiente")>
    Public Property ImportePdte As Decimal
    Public Property CCC As String
End Class

' Nesto#340 (1C.8, slice 5): petición/respuesta del PUT api/Clientes/CCCs.
Public Class GuardarCCCsRequest
    Public Property empresa As String
    Public Property cliente As String
    Public Property contacto As String
    Public Property cccActivo As String
    Public Property cccs As List(Of CCCModel)
    ' Carlos 17/11/26: con True, cccActivo pasa a ser la cuenta de la ficha del cliente (la de los recibos).
    Public Property establecerComoCuentaDelCliente As Boolean
End Class

Public Class GuardarCCCsRespuesta
    Public Property extractoOtroCCC As List(Of ExtractoCCCModel)
    Public Property pedidosOtroCCC As List(Of cabeceraPedidoAgrupada)
    ' Carlos 17/11/26: la cuenta que ha quedado en la ficha si se pidió establecerComoCuentaDelCliente.
    Public Property cccCliente As String
End Class

Public Class ExtractoClienteDTO
    Inherits ObservableObject
    Public Property Id As Integer
    Public Property Empresa As String
    Public Property Asiento As Integer
    Public Property Cliente As String
    Public Property Contacto As String
    Public Property Fecha As Date
    Public Property Tipo As String
    Public Property Documento As String
    Public Property Efecto As String
    Public Property Concepto As String
    Public Property Importe As Decimal
    Public Property ImportePendiente As Decimal
    Public Property Vendedor As String
    Public Property Vencimiento As Date
    Public Property CCC As String
    Public Property Ruta As String
    Public Property Estado As String
    Public Property FormaVenta As String
    Public Property Delegacion As String
    Public Property FormaPago As String
    Public Property Usuario As String
    Private _seleccionada As Boolean
    Public Property Seleccionada As Boolean
        Get
            Return _seleccionada
        End Get
        Set(value As Boolean)
            Dim unused = SetProperty(_seleccionada, value)
        End Set
    End Property

End Class

''' <summary>
''' DTO para pagos TPV (enlaces de pago NestoPago).
''' Issue Nesto#322: Histórico de enlaces de pago en ventana de Clientes.
''' </summary>
Public Class PagoTPVDTO
    Public Property Id As Integer
    Public Property NumeroOrden As String
    Public Property Tipo As String
    Public Property Empresa As String
    Public Property Cliente As String
    Public Property Contacto As String
    Public Property Importe As Decimal
    Public Property Descripcion As String
    Public Property Correo As String
    Public Property Movil As String
    Public Property Estado As String
    Public Property CodigoRespuesta As String
    Public Property CodigoAutorizacion As String
    Public Property FechaCreacion As DateTime
    Public Property FechaActualizacion As DateTime?
    Public Property Usuario As String
    Public Property Efectos As List(Of EfectoTPVDTO)
    Public Property PagoOriginalId As Integer?

    Public ReadOnly Property EstadoDescriptivo As String
        Get
            If PagoOriginalId.HasValue Then
                Return Estado & " (reintento)"
            End If
            Return Estado
        End Get
    End Property
End Class

Public Class EfectoTPVDTO
    Public Property Id As Integer
    Public Property ExtractoClienteId As Integer
    Public Property Importe As Decimal
    Public Property Documento As String
    Public Property Efecto As String
End Class