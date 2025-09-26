Imports System.Collections.ObjectModel
Imports Newtonsoft.Json
Imports Prism.Mvvm

Public Class ComisionAnualResumen
    Inherits BindableBase

    Public Property Id As Integer
    Public Property Vendedor As String
    Public Property Anno As Integer
    Public Property Mes As Integer
    <JsonProperty(ItemConverterType:=GetType(EtiquetaComisionConverter))>
    Public Property Etiquetas As Collection(Of IEtiquetaComision)
    Public Property TotalComisiones As Decimal
    Public Property TotalVentaAcumulada As Decimal
    Public Property TotalComisionAcumulada As Decimal
    Public Property TotalTipoAcumulado As Decimal

    ' Propiedades de conveniencia para acceder a la etiqueta General
    Public ReadOnly Property EtiquetaGeneral As IEtiquetaComisionAcumulada
        Get
            Return Etiquetas?.OfType(Of IEtiquetaComisionAcumulada)()?.FirstOrDefault()
        End Get
    End Property
End Class