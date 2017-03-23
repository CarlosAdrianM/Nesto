Imports System.ComponentModel

Public Class RapportsModel
    Public Class SeguimientoClienteDTO
        Public Property Id As Integer
        Public Property Empresa As String
        Public Property Cliente As String
        Public Property Contacto As String
        Public Property Fecha As DateTime
        Public Property Tipo As String
        Public Property Vendedor As String
        Public Property Pedido As Boolean
        Public Property ClienteNuevo As Boolean
        Public Property Aviso As Boolean
        Public Property Aparatos As Boolean
        Public Property GestionAparatos As Boolean
        Public Property PrimeraVisita As Boolean
        Public Property Comentarios As String
        Public Property Estado As EstadoSeguimientoDTO
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
    End Class

End Class
