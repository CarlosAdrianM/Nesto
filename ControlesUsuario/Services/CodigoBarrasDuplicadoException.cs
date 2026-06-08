using ControlesUsuario.Models;
using System;
using System.Collections.Generic;

namespace ControlesUsuario.Services
{
    /// <summary>
    /// Se lanza cuando un código de barras corresponde a varios productos y la API responde
    /// con 409 Conflict (NestoAPI#213). Lleva la lista de candidatos para que el cliente
    /// muestre un selector y reintente con el Número elegido. Nesto#368.
    /// </summary>
    public class CodigoBarrasDuplicadoException : Exception
    {
        public IReadOnlyList<ProductoCodigoBarrasDuplicado> Candidatos { get; }

        public CodigoBarrasDuplicadoException(IReadOnlyList<ProductoCodigoBarrasDuplicado> candidatos)
            : base("El código de barras corresponde a varios productos.")
        {
            Candidatos = candidatos ?? new List<ProductoCodigoBarrasDuplicado>();
        }
    }
}
