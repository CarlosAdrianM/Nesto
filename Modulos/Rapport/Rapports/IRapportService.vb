Imports System.Collections.ObjectModel
Imports Nesto.Modulos.Rapports

Public Interface IRapportService
    Function cargarListaRapports(empresa As String, cliente As String, contacto As String) As Task(Of ObservableCollection(Of RapportsModel.SeguimientoClienteDTO))
    Function cargarListaRapports(vendedor As String, fecha As Date) As Task(Of ObservableCollection(Of RapportsModel.SeguimientoClienteDTO))
End Interface
