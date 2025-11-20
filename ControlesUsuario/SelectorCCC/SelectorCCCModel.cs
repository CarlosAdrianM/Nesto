using Nesto.Infrastructure.Contracts;
using System;

namespace ControlesUsuario.Models
{
    /// <summary>
    /// Modelo para los CCCs (Códigos Cuenta Cliente / IBANs) usados en el SelectorCCC.
    /// Carlos 20/11/2024: Creado para el nuevo control SelectorCCC.
    /// </summary>
    public class CCCItem : IFiltrableItem
    {
        public string empresa { get; set; }
        public string cliente { get; set; }
        public string contacto { get; set; }
        public string numero { get; set; }
        public string pais { get; set; }
        public string entidad { get; set; }
        public string oficina { get; set; }
        public string bic { get; set; }
        public short estado { get; set; }
        public short? tipoMandato { get; set; }
        public DateTime? fechaMandato { get; set; }

        /// <summary>
        /// Indica si el CCC es válido (estado >= 0).
        /// CCCs con estado < 0 son inválidos y no deberían permitirse seleccionar.
        /// </summary>
        public bool EsValido => estado >= 0;

        /// <summary>
        /// Indica si el CCC es inválido (estado < 0).
        /// Usado para aplicar estilo en el ComboBox.
        /// </summary>
        public bool EsInvalido => estado < 0;

        /// <summary>
        /// Descripción formateada del CCC para mostrar en el ComboBox.
        /// Se establece dinámicamente al cargar la lista.
        /// </summary>
        public string Descripcion { get; set; }

        /// <summary>
        /// Texto formateado del IBAN para mostrar en el ComboBox.
        /// Alias de Descripcion para compatibilidad.
        /// </summary>
        public string TextoFormateado => Descripcion;

        /// <summary>
        /// Implementación de IFiltrableItem para búsqueda en el combo.
        /// </summary>
        public bool Contains(string filtro)
        {
            if (string.IsNullOrWhiteSpace(filtro))
                return true;

            filtro = filtro.ToLower();

            return (numero != null && numero.ToLower().Contains(filtro)) ||
                   (entidad != null && entidad.ToLower().Contains(filtro)) ||
                   (oficina != null && oficina.ToLower().Contains(filtro)) ||
                   (bic != null && bic.ToLower().Contains(filtro));
        }
    }
}
