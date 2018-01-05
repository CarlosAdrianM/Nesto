Imports Nesto.Models

Public Interface IAgenciaService
    Function CargarListaPendientes(context As NestoEntities) As IEnumerable(Of EnviosAgencia)
    Function GetEnvioById(Id As Integer) As EnviosAgencia
    Function Insertar(envio As EnviosAgencia) As EnviosAgencia
    Sub Modificar(envio As EnviosAgencia)
    Sub Borrar(Id As Integer)
End Interface
