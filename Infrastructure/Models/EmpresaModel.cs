using Newtonsoft.Json;

namespace Nesto.Infrastructure.Models
{
    /// <summary>
    /// Empresa ligera para combos (Nesto#340, Fase 1C.14 slice 1). El API serializa la
    /// entidad Empresa con nombres acentuados ("Número"); aquí se mapean a ASCII.
    /// Solo lectura en la UI, así que no necesita INotifyPropertyChanged.
    /// </summary>
    public class EmpresaModel
    {
        [JsonProperty("Número")]
        public string Numero { get; set; }
        public string Nombre { get; set; }
    }
}
