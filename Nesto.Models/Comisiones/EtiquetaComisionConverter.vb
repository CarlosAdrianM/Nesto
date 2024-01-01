Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq

Public Class EtiquetaComisionConverter
    Inherits JsonConverter

    Public Overrides Function CanConvert(objectType As Type) As Boolean
        Return GetType(IEtiquetaComision).IsAssignableFrom(objectType)
    End Function

    Public Overrides Function ReadJson(reader As JsonReader, objectType As Type, existingValue As Object, serializer As JsonSerializer) As Object
        Dim jo As JObject = JObject.Load(reader)

        If jo("Venta") IsNot Nothing Then
            Return jo.ToObject(Of EtiquetaComisionVenta)()
        ElseIf jo("Recuento") IsNot Nothing Then
            Return jo.ToObject(Of EtiquetaComisionClientes)()
        End If

        Throw New InvalidOperationException("No se puede determinar el tipo concreto de la etiqueta de comisión.")
    End Function

    Public Overrides Sub WriteJson(writer As JsonWriter, value As Object, serializer As JsonSerializer)
        Throw New NotImplementedException()
    End Sub
End Class
