Imports Nesto.Infrastructure.Models

' Nesto#340 Fase 1C.14: servicio API del módulo de Remesas (sustituye los accesos EF del VM).
Public Interface IRemesasService
    ' Slice 1: empresas para el combo de la cabecera.
    Function LeerEmpresas() As Task(Of List(Of EmpresaModel))
End Interface
