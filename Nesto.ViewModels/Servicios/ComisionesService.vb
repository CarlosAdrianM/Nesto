Imports System.Collections.ObjectModel
Imports System.Data.Entity
Imports System.Net.Http
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models
Imports Nesto.Models.Nesto.Models
Imports Newtonsoft.Json

Public Class ComisionesService
    Private ReadOnly configuracion As IConfiguracion
    Public Sub New(configuracion As IConfiguracion)
        Me.configuracion = configuracion
    End Sub

    Public Async Function LeerVendedores() As Task(Of List(Of VendedorDTO))
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage
            Dim respuesta As String = String.Empty
            Dim vendedor As String = Await configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.Vendedor)

            Try
                Dim urlConsulta As String = $"Vendedores?empresa={Constantes.Empresas.EMPRESA_DEFECTO}"
                If Not configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION) Then
                    urlConsulta += $"&vendedor={vendedor}"
                End If

                response = Await client.GetAsync(urlConsulta)
                If response.IsSuccessStatusCode Then
                    respuesta = Await response.Content.ReadAsStringAsync()
                Else
                    respuesta = String.Empty
                End If

            Catch ex As Exception
                Throw New Exception("No se ha podido recuperar la lista de vendedores")
            Finally

            End Try

            Dim listaVendedores As List(Of VendedorDTO) = JsonConvert.DeserializeObject(Of List(Of VendedorDTO))(respuesta)

            Return listaVendedores

        End Using
    End Function

    Friend Function LeerComisionesAntiguas(fechaDesde As Date, fechaHasta As Date, vendedor As String, v As Integer) As Comisiones_Result
        Using db = New NestoEntities
            Return db.Comisiones("1", fechaDesde, fechaHasta, vendedor, 0).FirstOrDefault
        End Using
    End Function

    Friend Function LeerListaPedidosVendedor(vendedor As String) As ObservableCollection(Of vstLinPedidoVtaConVendedor)
        Using db = New NestoEntities
            Return New ObservableCollection(Of vstLinPedidoVtaConVendedor)(From l In db.vstLinPedidoVtaConVendedor
                                                                           Where (l.Empresa = Constantes.Empresas.EMPRESA_DEFECTO Or l.Empresa = Constantes.Empresas.EMPRESA_ESPEJO) And l.Estado >= -1 And l.Estado <= 1 And l.Vendedor = vendedor
                                                                           Order By l.Número, l.Nº_Orden
                                                                           Select l)
        End Using
    End Function

    Friend Function LeerVentasVendedor(fechaDesde As Date, fechaHasta As Date, vendedor As String) As ObservableCollection(Of vstLinPedidoVtaComisiones)
        Using db = New NestoEntities
            Return New ObservableCollection(Of vstLinPedidoVtaComisiones)(From l In db.vstLinPedidoVtaComisiones
                                                                          Where (((l.Estado = 4 AndAlso l.Fecha_Factura >= fechaDesde AndAlso l.Fecha_Factura <= fechaHasta) OrElse
                                                                                             (l.Estado = 2 AndAlso l.Fecha_Albarán >= fechaDesde AndAlso l.Fecha_Albarán <= fechaHasta)) AndAlso
                                                                                             l.Vendedor = vendedor))

        End Using
    End Function
End Class
