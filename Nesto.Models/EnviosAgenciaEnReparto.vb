Imports Newtonsoft.Json

Namespace Nesto.Models

    Partial Public Class EnviosAgencia
        ''' <summary>
        ''' NestoAPI#516: envío tramitado que la agencia ya ha sacado a reparto (se entrega hoy, en principio).
        ''' Lo calcula la API a partir de DetalleEstado y llega en el listado de envíos; con una API antigua
        ''' llega False. No es un estado: el envío sigue en Tramitados. Solo lectura: no viaja de vuelta a la API.
        ''' </summary>
        <JsonIgnore>
        Public Property EnReparto As Boolean

        ''' <summary>
        ''' NestoAPI#880: reparto propio entregado con algún bulto de menos (DetalleEstado «Entregado (reparto propio): 4 de 5 bultos.
        ''' Falta RP…-5»). Lo calcula la API; con una API antigua llega False. Solo lectura.
        ''' </summary>
        <JsonIgnore>
        Public Property EntregaIncompleta As Boolean
    End Class

End Namespace
