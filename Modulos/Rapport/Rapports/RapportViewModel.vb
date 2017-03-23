Imports Microsoft.Practices.Prism.Regions
Imports Nesto.Contratos
Imports Nesto.Modulos.Rapports.RapportsModel
Imports Nesto.Modulos.Rapports.RapportsModel.SeguimientoClienteDTO

Public Class RapportViewModel
    Inherits ViewModelBase
    Implements INavigationAware

    Public Sub New(configuracion As IConfiguracion)
        Me.configuracion = configuracion
        listaTiposRapports = New List(Of TipoRapportVM)
        listaTiposRapports.Add(New TipoRapportVM With {
            .id = "V  ",
            .descripcion = "Visita"
        })
        listaTiposRapports.Add(New TipoRapportVM With {
            .id = "T  ",
            .descripcion = "Teléfono"
        })

    End Sub


    Public Property configuracion As IConfiguracion
    Private Const empresaPorDefecto As String = "1"

    Private _listaTiposRapports As List(Of TipoRapportVM)
    Public Property listaTiposRapports As List(Of TipoRapportVM)
        Get
            Return _listaTiposRapports
        End Get
        Set(value As List(Of TipoRapportVM))
            SetProperty(_listaTiposRapports, value)
        End Set
    End Property

    Private _rapport As SeguimientoClienteDTO
    Public Property rapport As SeguimientoClienteDTO
        Get
            Return _rapport
        End Get
        Set(value As SeguimientoClienteDTO)
            SetProperty(_rapport, value)
        End Set
    End Property



    Public Overloads Sub OnNavigatedTo(navigationContext As NavigationContext) Implements INavigationAware.OnNavigatedTo
        rapport = navigationContext.Parameters("rapportParameter")
    End Sub

    Public Structure TipoRapportVM
        Public Sub New(
       ByVal _id As String,
       ByVal _descripcion As String
       )
            id = _id
            descripcion = _descripcion
        End Sub
        Property id As String
        Property descripcion As String
    End Structure

End Class
