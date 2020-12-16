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

    Private _Estado As EstadoSeguimientoDTO
    Public Property Estado As EstadoSeguimientoDTO
        Get
            Return _Estado
        End Get
        Set(value As EstadoSeguimientoDTO)
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

