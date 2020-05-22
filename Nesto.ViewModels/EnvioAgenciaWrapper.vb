Imports Microsoft.Practices.Prism.Mvvm
Imports Nesto.Models
Imports Nesto.Models.Nesto.Models

Public Class EnvioAgenciaWrapper
    Inherits BindableBase

    Private _numero As Integer
    Public Property Numero As Integer
        Get
            Return _numero
        End Get
        Set(value As Integer)
            SetProperty(_numero, value)
        End Set
    End Property

    Private _empresa As String
    Public Property Empresa As String
        Get
            Return _empresa
        End Get
        Set(value As String)
            SetProperty(_empresa, value)
        End Set
    End Property

    Private _agencia As Integer
    Public Property Agencia As Integer
        Get
            Return _agencia
        End Get
        Set(value As Integer)
            SetProperty(_agencia, value)
        End Set
    End Property

    Private _cliente As String
    Public Property Cliente As String
        Get
            Return _cliente
        End Get
        Set(value As String)
            SetProperty(_cliente, value)
        End Set
    End Property

    Private _contacto As String
    Public Property Contacto As String
        Get
            Return _contacto
        End Get
        Set(value As String)
            SetProperty(_contacto, value)
        End Set
    End Property

    Private _pedido As Integer
    Public Property Pedido As Integer
        Get
            Return _pedido
        End Get
        Set(value As Integer)
            SetProperty(_pedido, value)
        End Set
    End Property

    Private _estado As Short
    Public Property Estado As Short
        Get
            Return _estado
        End Get
        Set(value As Short)
            SetProperty(_estado, value)
        End Set
    End Property

    Private _fecha As DateTime
    Public Property Fecha As DateTime
        Get
            Return _fecha
        End Get
        Set(value As DateTime)
            SetProperty(_fecha, value)
        End Set
    End Property

    Private _servicio As Byte
    Public Property Servicio As Byte
        Get
            Return _servicio
        End Get
        Set(value As Byte)
            SetProperty(_servicio, value)
        End Set
    End Property


    Private _horario As Integer
    Public Property Horario As Integer
        Get
            Return _horario
        End Get
        Set(value As Integer)
            SetProperty(_horario, value)
        End Set
    End Property


    Private _bultos As Integer
    Public Property Bultos As Integer
        Get
            Return _bultos
        End Get
        Set(value As Integer)
            SetProperty(_bultos, value)
        End Set
    End Property

    Private _retorno As Integer
    Public Property Retorno As Integer
        Get
            Return _retorno
        End Get
        Set(value As Integer)
            SetProperty(_retorno, value)
        End Set
    End Property

    Private _nombre As String
    Public Property Nombre As String
        Get
            Return _nombre
        End Get
        Set(value As String)
            SetProperty(_nombre, value)
        End Set
    End Property

    Private _direccion As String
    Public Property Direccion As String
        Get
            Return _direccion
        End Get
        Set(value As String)
            SetProperty(_direccion, value)
        End Set
    End Property

    Private _codigoPostal As String
    Public Property CodPostal As String
        Get
            Return _codigoPostal
        End Get
        Set(value As String)
            SetProperty(_codigoPostal, value)
        End Set
    End Property

    Private _poblacion As String
    Public Property Poblacion As String
        Get
            Return _poblacion
        End Get
        Set(value As String)
            SetProperty(_poblacion, value)
        End Set
    End Property

    Private _provincia As String
    Public Property Provincia As String
        Get
            Return _provincia
        End Get
        Set(value As String)
            SetProperty(_provincia, value)
        End Set
    End Property

    Private _telefono As String
    Public Property Telefono As String
        Get
            Return _telefono
        End Get
        Set(value As String)
            SetProperty(_telefono, value)
        End Set
    End Property

    Private _movil As String
    Public Property Movil As String
        Get
            Return _movil
        End Get
        Set(value As String)
            SetProperty(_movil, value)
        End Set
    End Property

    Private _email As String
    Public Property Email As String
        Get
            Return _email
        End Get
        Set(value As String)
            SetProperty(_email, value)
        End Set
    End Property

    Private _observaciones As String
    Public Property Observaciones As String
        Get
            Return _observaciones
        End Get
        Set(value As String)
            SetProperty(_observaciones, value)
        End Set
    End Property

    Private _atencion As String
    Public Property Atencion As String
        Get
            Return _atencion
        End Get
        Set(value As String)
            SetProperty(_atencion, value)
        End Set
    End Property

    Private _reembolso As Decimal
    Public Property Reembolso As Decimal
        Get
            Return _reembolso
        End Get
        Set(value As Decimal)
            SetProperty(_reembolso, value)
        End Set
    End Property

    Private _fechaEntrega As DateTime
    Public Property FechaEntrega As DateTime
        Get
            Return _fechaEntrega
        End Get
        Set(value As DateTime)
            SetProperty(_fechaEntrega, value)
        End Set
    End Property


    ' Mirar si se puede implementar como un change tracker estándar
    Private _tieneCambios As Boolean
    Public Property TieneCambios As Boolean
        Get
            Return _tieneCambios
        End Get
        Set(value As Boolean)
            SetProperty(_tieneCambios, value)
        End Set
    End Property

    Private _pais As Integer
    Public Property Pais As Integer
        Get
            Return _pais
        End Get
        Set(value As Integer)
            SetProperty(_pais, value)
        End Set
    End Property

    Public Property PaisISO As String

    Private _codigoBarras As String
    Public Property CodigoBarras As String
        Get
            Return _codigoBarras
        End Get
        Set(value As String)
            SetProperty(_codigoBarras, value)
        End Set
    End Property

    Public Property RowVersion As Byte()


    Public Function ToEnvioAgencia() As EnviosAgencia
        Return New EnviosAgencia With {
            .Agencia = Agencia,
            .Atencion = Atencion,
            .Bultos = Bultos,
            .Cliente = Cliente,
            .CodigoBarras = CodigoBarras,
            .CodPostal = CodPostal,
            .Contacto = Contacto,
            .Direccion = Direccion?.Replace("""", String.Empty),
            .Email = Email,
            .Empresa = Empresa,
            .Estado = Estado,
            .Fecha = Fecha,
            .FechaEntrega = FechaEntrega,
            .Horario = Horario,
            .Movil = Movil,
            .Nombre = Nombre?.Replace("""", String.Empty),
            .Numero = Numero,
            .Observaciones = Observaciones?.Replace("""", String.Empty),
            .Pais = Pais,
            .Pedido = Pedido,
            .Poblacion = Poblacion,
            .Provincia = Provincia,
            .Reembolso = Reembolso,
            .Retorno = Retorno,
            .Servicio = Servicio,
            .Telefono = Telefono,
            .RowVersion = RowVersion
        }
    End Function


    Public Shared Function EnvioAgenciaAWrapper(envio As EnviosAgencia) As EnvioAgenciaWrapper
        Return New EnvioAgenciaWrapper With {
            .Agencia = envio.Agencia,
            .Atencion = envio.Atencion,
            .Bultos = envio.Bultos,
            .Cliente = envio.Cliente,
            .CodigoBarras = envio.CodigoBarras,
            .CodPostal = envio.CodPostal,
            .Contacto = envio.Contacto,
            .Direccion = envio.Direccion,
            .Email = envio.Email,
            .Empresa = envio.Empresa,
            .Estado = envio.Estado,
            .Fecha = envio.Fecha,
            .FechaEntrega = envio.FechaEntrega,
            .Horario = envio.Horario,
            .Movil = envio.Movil,
            .Nombre = envio.Nombre,
            .Numero = envio.Numero,
            .Observaciones = envio.Observaciones,
            .Pais = envio.Pais,
            .Pedido = envio.Pedido,
            .Poblacion = envio.Poblacion,
            .Provincia = envio.Provincia,
            .Reembolso = envio.Reembolso,
            .Retorno = envio.Retorno,
            .Servicio = envio.Servicio,
            .Telefono = envio.Telefono,
            .RowVersion = envio.RowVersion
        }
    End Function

End Class
