Imports System.IO
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Models
Imports Newtonsoft.Json

''' <summary>
''' Implementación del servicio de borradores que guarda en archivos JSON locales.
''' Issue #286: Borradores de PlantillaVenta
'''
''' Los borradores se guardan en: %LOCALAPPDATA%\Nesto\Borradores\
''' Cada borrador es un archivo JSON con nombre: {GUID}.json
''' </summary>
Public Class BorradorPlantillaVentaService
    Implements IBorradorPlantillaVentaService

    Private ReadOnly _configuracion As IConfiguracion
    Private ReadOnly _carpetaBorradores As String

    Private Shared ReadOnly JsonSettings As New JsonSerializerSettings With {
        .Formatting = Formatting.Indented,
        .NullValueHandling = NullValueHandling.Include,
        .ReferenceLoopHandling = ReferenceLoopHandling.Ignore
    }

    Public Sub New(configuracion As IConfiguracion)
        _configuracion = configuracion

        ' Carpeta: %LOCALAPPDATA%\Nesto\Borradores
        _carpetaBorradores = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Nesto",
            "Borradores")

        ' Crear carpeta si no existe
        If Not Directory.Exists(_carpetaBorradores) Then
            Directory.CreateDirectory(_carpetaBorradores)
        End If
    End Sub

    Public Function GuardarBorrador(pedido As PedidoVentaDTO, nombreCliente As String, Optional mensajeError As String = Nothing) As BorradorPlantillaVenta Implements IBorradorPlantillaVentaService.GuardarBorrador
        Try
            Dim borrador As New BorradorPlantillaVenta With {
                .Id = Guid.NewGuid().ToString(),
                .FechaCreacion = DateTime.Now,
                .Cliente = pedido?.cliente,
                .NombreCliente = nombreCliente,
                .Total = If(pedido?.Lineas IsNot Nothing, pedido.Lineas.Sum(Function(l) l.Total), 0),
                .NumeroLineas = If(pedido?.Lineas IsNot Nothing, pedido.Lineas.Count, 0),
                .Usuario = _configuracion?.usuario,
                .MensajeError = mensajeError,
                .Pedido = pedido
            }

            Dim rutaArchivo As String = Path.Combine(_carpetaBorradores, $"{borrador.Id}.json")
            Dim json As String = JsonConvert.SerializeObject(borrador, JsonSettings)
            File.WriteAllText(rutaArchivo, json)

            System.Diagnostics.Debug.WriteLine($"✓ Borrador guardado: {rutaArchivo}")

            Return borrador
        Catch ex As Exception
            System.Diagnostics.Debug.WriteLine($"❌ Error al guardar borrador: {ex.Message}")
            Throw
        End Try
    End Function

    Public Function ObtenerBorradores() As List(Of BorradorPlantillaVenta) Implements IBorradorPlantillaVentaService.ObtenerBorradores
        Dim borradores As New List(Of BorradorPlantillaVenta)

        Try
            If Not Directory.Exists(_carpetaBorradores) Then
                Return borradores
            End If

            For Each archivo In Directory.GetFiles(_carpetaBorradores, "*.json")
                Try
                    ' Cargar solo metadatos (sin el pedido completo para ahorrar memoria)
                    Dim json As String = File.ReadAllText(archivo)
                    Dim borrador = JsonConvert.DeserializeObject(Of BorradorPlantillaVenta)(json, JsonSettings)
                    If borrador IsNot Nothing Then
                        ' Limpiar el pedido para ahorrar memoria en la lista
                        ' Se cargará completo cuando se seleccione
                        borrador.Pedido = Nothing
                        borradores.Add(borrador)
                    End If
                Catch ex As Exception
                    System.Diagnostics.Debug.WriteLine($"❌ Error al leer borrador {archivo}: {ex.Message}")
                End Try
            Next

            ' Ordenar por fecha descendente (más reciente primero)
            Return borradores.OrderByDescending(Function(b) b.FechaCreacion).ToList()
        Catch ex As Exception
            System.Diagnostics.Debug.WriteLine($"❌ Error al obtener borradores: {ex.Message}")
            Return borradores
        End Try
    End Function

    Public Function CargarBorrador(id As String) As BorradorPlantillaVenta Implements IBorradorPlantillaVentaService.CargarBorrador
        Try
            Dim rutaArchivo As String = Path.Combine(_carpetaBorradores, $"{id}.json")

            If Not File.Exists(rutaArchivo) Then
                Return Nothing
            End If

            Dim json As String = File.ReadAllText(rutaArchivo)
            Return JsonConvert.DeserializeObject(Of BorradorPlantillaVenta)(json, JsonSettings)
        Catch ex As Exception
            System.Diagnostics.Debug.WriteLine($"❌ Error al cargar borrador {id}: {ex.Message}")
            Return Nothing
        End Try
    End Function

    Public Function EliminarBorrador(id As String) As Boolean Implements IBorradorPlantillaVentaService.EliminarBorrador
        Try
            Dim rutaArchivo As String = Path.Combine(_carpetaBorradores, $"{id}.json")

            If File.Exists(rutaArchivo) Then
                File.Delete(rutaArchivo)
                System.Diagnostics.Debug.WriteLine($"✓ Borrador eliminado: {rutaArchivo}")
                Return True
            End If

            Return False
        Catch ex As Exception
            System.Diagnostics.Debug.WriteLine($"❌ Error al eliminar borrador {id}: {ex.Message}")
            Return False
        End Try
    End Function

    Public Function EliminarTodosBorradores() As Integer Implements IBorradorPlantillaVentaService.EliminarTodosBorradores
        Dim eliminados As Integer = 0

        Try
            If Not Directory.Exists(_carpetaBorradores) Then
                Return 0
            End If

            For Each archivo In Directory.GetFiles(_carpetaBorradores, "*.json")
                Try
                    File.Delete(archivo)
                    eliminados += 1
                Catch ex As Exception
                    System.Diagnostics.Debug.WriteLine($"❌ Error al eliminar {archivo}: {ex.Message}")
                End Try
            Next

            System.Diagnostics.Debug.WriteLine($"✓ {eliminados} borradores eliminados")
        Catch ex As Exception
            System.Diagnostics.Debug.WriteLine($"❌ Error al eliminar todos los borradores: {ex.Message}")
        End Try

        Return eliminados
    End Function

    Public Function ContarBorradores() As Integer Implements IBorradorPlantillaVentaService.ContarBorradores
        Try
            If Not Directory.Exists(_carpetaBorradores) Then
                Return 0
            End If

            Return Directory.GetFiles(_carpetaBorradores, "*.json").Length
        Catch ex As Exception
            System.Diagnostics.Debug.WriteLine($"❌ Error al contar borradores: {ex.Message}")
            Return 0
        End Try
    End Function
End Class
