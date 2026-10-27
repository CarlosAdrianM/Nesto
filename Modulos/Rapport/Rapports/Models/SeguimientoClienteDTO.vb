Imports System.ComponentModel
Imports System.Runtime.CompilerServices

Public Class SeguimientoClienteDTO
    Implements INotifyPropertyChanged

    Private _Id As Integer
    Public Property Id As Integer
        Get
            Return _Id
        End Get
        Set(value As Integer)
            If _Id <> value Then
                _Id = value
                RaisePropertyChanged("Id")
            End If
        End Set
    End Property

    Private _Empresa As String
    Public Property Empresa As String
        Get
            Return _Empresa
        End Get
        Set(value As String)
            If _Empresa <> value Then
                _Empresa = value
                RaisePropertyChanged("Empresa")
            End If
        End Set
    End Property

    Private _Cliente As String
    Public Property Cliente As String
        Get
            Return _Cliente
        End Get
        Set(value As String)
            If _Cliente <> value Then
                _Cliente = value
                RaisePropertyChanged("Cliente")
            End If
        End Set
    End Property
    Public Property Contacto As String

    Private _Fecha As DateTime
    Public Property Fecha As DateTime
        Get
            Return _Fecha
        End Get
        Set(value As DateTime)
            If _Fecha <> value Then
                _Fecha = value
                RaisePropertyChanged("Fecha")
            End If
        End Set
    End Property

    Private _Tipo As String
    Public Property Tipo As String
        Get
            Return _Tipo
        End Get
        Set(value As String)
            If _Tipo <> value Then
                _Tipo = value
                RaisePropertyChanged("Tipo")
            End If
        End Set
    End Property

    Private _Vendedor As String
    Public Property Vendedor As String
        Get
            Return _Vendedor
        End Get
        Set(value As String)
            If _Vendedor <> value Then
                _Vendedor = value
                RaisePropertyChanged("Vendedor")
            End If
        End Set
    End Property

    Private _Pedido As Boolean
    Public Property Pedido As Boolean
        Get
            Return _Pedido
        End Get
        Set(value As Boolean)
            If _Pedido <> value Then
                _Pedido = value
                RaisePropertyChanged("Pedido")
            End If
        End Set
    End Property
    Public Property ClienteNuevo As Boolean
    Public Property Aviso As Boolean
    Public Property Aparatos As Boolean
    Public Property GestionAparatos As Boolean
    Public Property PrimeraVisita As Boolean

    Private _Comentarios As String
    Public Property Comentarios As String
        Get
            Return _Comentarios
        End Get
        Set(value As String)
            If _Comentarios <> value Then
                _Comentarios = value
                RaisePropertyChanged("Comentarios")
            End If
        End Set
    End Property

    Private _Estado As Short
    Public Property Estado As Short
        Get
            Return _Estado
        End Get
        Set(value As Short)
            If _Estado <> value Then
                _Estado = value
                RaisePropertyChanged("Estado")
            End If
        End Set
    End Property
    Public Property Usuario As String
    Public Property TipoCentro As TiposCentro
    Public Property Nombre As String
    Public Property Direccion As String
    Public Property NumOrdenExtracto As Integer?
    ''' <summary>
    ''' NestoAPI#464 / Nesto#469: empleados del centro (0 = sin empleados, 1..4, 5 = "5 o mas").
    ''' Nothing = el vendedor no lo ha tocado y la API no cambia la ficha.
    ''' </summary>
    Public Property Empleados As Byte?

    ''' <summary>
    ''' Nesto#558 (NestoAPI#716): resultado del rapport / motivo de no compra, un código del catálogo
    ''' Nesto.Infrastructure.Shared.ResultadoRapport (Pedido, PediraMasTarde, NoNecesita...). Nothing = sin marcar
    ''' (viaja null y la API no guarda nada). Un valor fuera del catálogo es un 400 de la API.
    ''' </summary>
    Private _Resultado As String
    Public Property Resultado As String
        Get
            Return _Resultado
        End Get
        Set(value As String)
            Dim limpio As String = If(String.IsNullOrWhiteSpace(value), Nothing, value.Trim())
            If _Resultado <> limpio Then
                _Resultado = limpio
                RaisePropertyChanged("Resultado")
            End If
        End Set
    End Property

    ''' <summary>Nesto#558 (NestoAPI#716): cuándo toca volver a contactar (solo la fecha). Nothing = sin fecha.</summary>
    Private _ProximoContacto As DateTime?
    Public Property ProximoContacto As DateTime?
        Get
            Return _ProximoContacto
        End Get
        Set(value As DateTime?)
            Dim soloFecha As DateTime? = If(value.HasValue, value.Value.Date, CType(Nothing, DateTime?))
            If Not Nullable.Equals(_ProximoContacto, soloFecha) Then
                _ProximoContacto = soloFecha
                RaisePropertyChanged("ProximoContacto")
            End If
        End Set
    End Property

    Public Enum TiposCentro
        <Description("No se sabe")>
        NoSeSabe
        <Description("Sólo Estética")>
        SoloEstetica
        <Description("Sólo Peluquería")>
        SoloPeluqueria
        <Description("Estética y Peluquería")>
        EsteticaYPeluqueria
    End Enum

    Public Enum EstadoSeguimientoDTO
        Nulo = -1
        Vigente
        No_Contactado
        Gestion_Administrativa
    End Enum

    Public Class TipoSeguimientoDTO
        Public Const TELEFONO As String = "T"
        Public Const VISITA As String = "V"
    End Class

    Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged

    Private Sub RaisePropertyChanged(<CallerMemberName()> Optional ByVal propertyName As String = Nothing)
        RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(propertyName))
    End Sub
End Class

