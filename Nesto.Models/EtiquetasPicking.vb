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

    Partial Public Class EtiquetasPicking
        Public Property Empresa As String
        Public Property Número As Integer
        Public Property Picking As Integer
        Public Property NºBultos As Nullable(Of Integer)
        Public Property UsuarioQuePrepara As Nullable(Of Integer)
        Public Property NºCliente As String
        Public Property Contacto As String
        Public Property Usuario As String
        Public Property FechaModificacion As Date
    
        Public Overridable Property Empresas As Empresas
        Public Overridable Property MultiUsuarios As MultiUsuarios
    
    End Class

End Namespace
