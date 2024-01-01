Imports System.Collections.ObjectModel
Imports System.Windows.Media
Imports Prism.Mvvm
Imports Newtonsoft.Json

Public Class ComisionAnualResumen
    Inherits BindableBase

    Public Property Id As Integer
    Public Property Vendedor As String
    Public Property Anno As Integer
    Public Property Mes As Integer
    <JsonProperty(ItemConverterType:=GetType(EtiquetaComisionConverter))>
    Public Property Etiquetas As Collection(Of IEtiquetaComision)
    Public Property GeneralProyeccion As Decimal
    Public Property GeneralFaltaParaSalto As Decimal
    Public Property GeneralInicioTramo As Decimal
    Public Property GeneralFinalTramo As Decimal
    Public Property GeneralBajaSaltoMesSiguiente As Boolean
    Public Property GeneralVentaAcumulada As Decimal
    Public Property GeneralComisionAcumulada As Decimal
    Public Property GeneralTipoConseguido As Decimal
    Public Property GeneralTipoReal As Decimal
    Public ReadOnly Property GeneralTipoConseguidoYReal As String
        Get
            Return String.Format("{0} / {1}", GeneralTipoConseguido.ToString("P"), GeneralTipoReal.ToString("P"))
        End Get
    End Property
    Public Property TotalComisiones As Decimal

    Public ReadOnly Property ColorProgreso As Brush
        Get
            If GeneralBajaSaltoMesSiguiente Then
                Return Brushes.Red
            Else
                Return Brushes.Green
            End If
        End Get
    End Property

    Public ReadOnly Property ColorTipoConseguidoYReal As Brush
        Get

            If GeneralTipoReal = GeneralTipoConseguido Then
                Return Brushes.Green
            Else
                Return Brushes.Black
            End If
        End Get
    End Property
End Class
