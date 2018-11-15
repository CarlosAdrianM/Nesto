Imports System.Collections.ObjectModel
Imports Nesto.Modulos.Rapports
Imports Nesto.Modulos.Rapports.RapportsModel

Public Interface IRapportService
    Function cargarListaRapports(empresa As String, cliente As String, contacto As String) As Task(Of ObservableCollection(Of RapportsModel.SeguimientoClienteDTO))
    Function cargarListaRapports(vendedor As String, fecha As Date) As Task(Of ObservableCollection(Of RapportsModel.SeguimientoClienteDTO))
    Function crearRapport(rapport As SeguimientoClienteDTO) As Task(Of String)
    Function CrearCita(rapport As SeguimientoClienteDTO, fechaAviso As Date) As Task(Of String)
End Interface
