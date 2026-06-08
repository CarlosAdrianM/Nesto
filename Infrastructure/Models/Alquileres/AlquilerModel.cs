using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace Nesto.Infrastructure.Models.Alquileres
{
    /// <summary>
    /// Cabecera de alquiler (tabla CabAlquileres) editable en el grid principal de Alquileres.
    /// Nesto#340 Fase 1C.3: sustituye la entidad EF CabAlquileres del cliente. Las propiedades
    /// conservan los nombres con acentos/ñ que usa la vista (Número, FechaSeñal, ...) y se mapean
    /// con [JsonProperty] a los nombres ASCII del DTO de la API. Implementa INotifyPropertyChanged
    /// para que la edición TwoWay marque cambios pendientes (sustituye el ChangeTracker de EF).
    /// Los campos *Cliente son de solo lectura (etiqueta del pedido) y la API los ignora al guardar.
    /// </summary>
    public class AlquilerModel : INotifyPropertyChanged
    {
        // Clave / identidad
        public string Empresa { get; set; }

        [JsonProperty("Numero")]
        public int Número { get => _numero; set => SetProperty(ref _numero, value); }
        private int _numero;

        // Campos editables
        public string Cliente { get => _cliente; set => SetProperty(ref _cliente, value); }
        private string _cliente;

        public string Contacto { get => _contacto; set => SetProperty(ref _contacto, value); }
        private string _contacto;

        public string Producto { get => _producto; set => SetProperty(ref _producto, value); }
        private string _producto;

        public string Inmovilizado { get => _inmovilizado; set => SetProperty(ref _inmovilizado, value); }
        private string _inmovilizado;

        public int? Cuotas { get => _cuotas; set => SetProperty(ref _cuotas, value); }
        private int? _cuotas;

        public DateTime? FechaEntrega { get => _fechaEntrega; set => SetProperty(ref _fechaEntrega, value); }
        private DateTime? _fechaEntrega;

        [JsonProperty("FechaSenal")]
        public DateTime? FechaSeñal { get => _fechaSenal; set => SetProperty(ref _fechaSenal, value); }
        private DateTime? _fechaSenal;

        [JsonProperty("ImporteSenal")]
        public decimal? ImporteSeñal { get => _importeSenal; set => SetProperty(ref _importeSenal, value); }
        private decimal? _importeSenal;

        public string NumeroSerie { get => _numeroSerie; set => SetProperty(ref _numeroSerie, value); }
        private string _numeroSerie;

        [JsonProperty("SenalComisiona")]
        public bool? SeñalComisiona { get => _senalComisiona; set => SetProperty(ref _senalComisiona, value); }
        private bool? _senalComisiona;

        [JsonProperty("Indemnizacion")]
        public decimal? Indemnización { get => _indemnizacion; set => SetProperty(ref _indemnizacion, value); }
        private decimal? _indemnizacion;

        public decimal? Importe { get => _importe; set => SetProperty(ref _importe, value); }
        private decimal? _importe;

        public int? CabPedidoVta { get => _cabPedidoVta; set => SetProperty(ref _cabPedidoVta, value); }
        private int? _cabPedidoVta;

        public string RutaContrato { get => _rutaContrato; set => SetProperty(ref _rutaContrato, value); }
        private string _rutaContrato;

        public string Comentarios { get => _comentarios; set => SetProperty(ref _comentarios, value); }
        private string _comentarios;

        // Solo lectura (display / etiquetas). La API los ignora al guardar.
        public string NombreProducto { get; set; }
        public string Familia { get; set; }
        public string NombreCliente { get; set; }
        public string DireccionCliente { get; set; }
        public string CodPostalCliente { get; set; }
        public string PoblacionCliente { get; set; }
        public string ProvinciaCliente { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        private void SetProperty<T>(ref T campo, T valor, [CallerMemberName] string propiedad = null)
        {
            if (Equals(campo, valor))
            {
                return;
            }
            campo = valor;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propiedad));
        }
    }
}
