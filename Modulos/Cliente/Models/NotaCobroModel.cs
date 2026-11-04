using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Nesto.Modulos.Cliente.Models
{
    /// <summary>
    /// Nesto#618 (NestoAPI#786): lo que se manda en POST api/NotasEntrega/Cobro (CrearNotaCobroDTO de NestoAPI).
    /// </summary>
    public class CrearNotaCobroModel
    {
        /// <summary>Solo la 1 (el reparto propio).</summary>
        public string Empresa { get; set; }
        public string Cliente { get; set; }
        /// <summary>Vacío: el del primer efecto.</summary>
        public string Contacto { get; set; }
        /// <summary>Nº Orden de ExtractoCliente, en el orden en que se liquidan (máx. 50).</summary>
        public List<int> Efectos { get; set; } = new List<int>();
        /// <summary>Sin él, la suma de lo pendiente de los efectos.</summary>
        public decimal? Importe { get; set; }
        /// <summary>Desde qué día sale en el reparto (nunca antes de hoy).</summary>
        public DateTime? Fecha { get; set; }
        /// <summary>Para el repartidor (máx. 500).</summary>
        public string Comentario { get; set; }
        /// <summary>NestoAPI#786: el repartidor que va a cobrarla (Usuario de GET api/RepartoPropio/Repartidores). Obligatorio.</summary>
        public string Repartidor { get; set; }
    }

    /// <summary>NestoAPI#786: un repartidor de GET api/RepartoPropio/Repartidores (RepartidorDTO de NestoAPI).</summary>
    public class RepartidorModel
    {
        /// <summary>El usuario sin dominio («Miguel»): lo que se manda en <see cref="CrearNotaCobroModel.Repartidor"/>.</summary>
        public string Usuario { get; set; }
        public string Nombre { get; set; }
    }

    /// <summary>Nesto#618: la respuesta (201) de POST api/NotasEntrega/Cobro (NotaCobroDTO de NestoAPI).</summary>
    public class NotaCobroCreadaModel
    {
        public string Empresa { get; set; }
        public int Pedido { get; set; }
        public string Cliente { get; set; }
        public string Contacto { get; set; }
        public string Nombre { get; set; }
        public string Ruta { get; set; }
        public decimal Importe { get; set; }
        public List<EfectoNotaCobroModel> Efectos { get; set; } = new List<EfectoNotaCobroModel>();
        /// <summary>«NC{Pedido}».</summary>
        public string CodigoDocumento { get; set; }
        public int EnvioId { get; set; }
        public DateTime Fecha { get; set; }
        /// <summary>Lo que ve el repartidor.</summary>
        public string Texto { get; set; }
        /// <summary>NestoAPI#786: el repartidor al que se ha asignado (sin dominio).</summary>
        public string Repartidor { get; set; }
    }

    /// <summary>Nesto#618: un efecto de la nota de cobro creada (EfectoNotaCobroDTO de NestoAPI).</summary>
    public class EfectoNotaCobroModel
    {
        public int Id { get; set; }
        public string Empresa { get; set; }
        public string Documento { get; set; }
        public string Efecto { get; set; }
        public DateTime Vencimiento { get; set; }
        public decimal Importe { get; set; }
        public decimal ImportePendiente { get; set; }
    }

    /// <summary>
    /// Nesto#618: una fila del extracto pendiente en el diálogo «Nueva nota de cobro». <see cref="Marcado"/> es la casilla y
    /// <see cref="Orden"/> el puesto en que se marcó (1, 2…; 0 sin marcar): los efectos se cobran en ese orden.
    /// </summary>
    public class EfectoACobrarModel : INotifyPropertyChanged
    {
        public EfectoACobrarModel(ExtractoClienteModel extracto)
        {
            Extracto = extracto ?? throw new ArgumentNullException(nameof(extracto));
        }

        public ExtractoClienteModel Extracto { get; }

        public int Id => Extracto.Id;
        public string Empresa => Extracto.Empresa?.Trim();
        public string Contacto => Extracto.Contacto?.Trim();
        public DateTime Fecha => Extracto.Fecha;
        public string Tipo => Extracto.Tipo?.Trim();
        public string Documento => Extracto.Documento?.Trim();
        public string Efecto => Extracto.Efecto?.Trim();
        public string Concepto => Extracto.Concepto?.Trim();
        public decimal Importe => Extracto.Importe;
        public decimal ImportePendiente => Extracto.ImportePendiente;
        public DateTime? Vencimiento => Extracto.Vencimiento;
        public string FormaPago => Extracto.FormaPago?.Trim();

        private bool _marcado;
        public bool Marcado
        {
            get => _marcado;
            set
            {
                if (_marcado != value)
                {
                    _marcado = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Marcado)));
                }
            }
        }

        private int _orden;
        /// <summary>El puesto en que se marcó (1, 2…); 0 si no está marcado. Lo pone el ViewModel del diálogo.</summary>
        public int Orden
        {
            get => _orden;
            internal set
            {
                if (_orden != value)
                {
                    _orden = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Orden)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TextoOrden)));
                }
            }
        }

        /// <summary>«1», «2»… o vacío.</summary>
        public string TextoOrden => Orden > 0 ? Orden.ToString() : string.Empty;

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
