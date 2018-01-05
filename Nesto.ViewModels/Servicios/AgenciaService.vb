Imports Nesto.Models
Imports Nesto.ViewModels

Public Class AgenciaService
    Implements IAgenciaService

    Public Sub Modificar(envio As EnviosAgencia) Implements IAgenciaService.Modificar
        Throw New NotImplementedException()
    End Sub

    Public Sub Borrar(Id As Integer) Implements IAgenciaService.Borrar
        Throw New NotImplementedException()
    End Sub

    Public Function CargarListaPendientes(contexto As NestoEntities) As IEnumerable(Of EnviosAgencia) Implements IAgenciaService.CargarListaPendientes
        Return contexto.EnviosAgencia.Where(Function(e) e.Estado < 0)
    End Function

    Public Function GetEnvioById(Id As Integer) As EnviosAgencia Implements IAgenciaService.GetEnvioById
        Throw New NotImplementedException()
    End Function

    Public Function Insertar(envio As EnviosAgencia) As EnviosAgencia Implements IAgenciaService.Insertar
        Throw New NotImplementedException()
    End Function

End Class
