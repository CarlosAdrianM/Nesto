'------------------------------------------------------------------------------
' <auto-generated>
'     Este código se generó a partir de una plantilla.
'
'     Los cambios manuales en este archivo pueden causar un comportamiento inesperado de la aplicación.
'     Los cambios manuales en este archivo se sobrescribirán si se regenera el código.
' </auto-generated>
'------------------------------------------------------------------------------

Imports System
Imports System.Collections.Generic

Namespace Nesto.Models

    Partial Public Class ExtractoCliente
        Public Property Empresa As String
        Public Property Nº_Orden As Integer
        Public Property Asiento As Integer
        Public Property Número As String
        Public Property Contacto As String
        Public Property Fecha As Date
        Public Property TipoApunte As String
        Public Property Nº_Documento As String
        Public Property Efecto As String
        Public Property Concepto As String
        Public Property Importe As Decimal
        Public Property ImportePdte As Decimal
        Public Property Delegación As String
        Public Property FormaVenta As String
        Public Property Vendedor As String
        Public Property FechaVto As Nullable(Of Date)
        Public Property Liquidado As Nullable(Of Integer)
        Public Property FormaPago As String
        Public Property Remesa As Nullable(Of Integer)
        Public Property CIF_NIF As String
        Public Property CCC As String
        Public Property Ruta As String
        Public Property Origen As String
        Public Property Estado As String
        Public Property Usuario As String
        Public Property Fecha_Modificación As Date
    
        Public Overridable Property CCC1 As CCC
        Public Overridable Property Clientes As Clientes
        Public Overridable Property Empresas As Empresas
        Public Overridable Property Empresas1 As Empresas
        Public Overridable Property Remesas As Remesas
        Public Overridable Property Vendedores As Vendedores
        Public Overridable Property Rutas As Rutas
    
    End Class

End Namespace