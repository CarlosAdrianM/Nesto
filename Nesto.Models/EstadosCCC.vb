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

    Partial Public Class EstadosCCC
        Public Property Empresa As String
        Public Property Número As Short
        Public Property Descripción As String
        Public Property Usuario As String
        Public Property Fecha_Modificación As Date
    
        Public Overridable Property CCC As ICollection(Of CCC) = New HashSet(Of CCC)
    
    End Class

End Namespace
