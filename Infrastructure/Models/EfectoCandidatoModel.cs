using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Nesto.Infrastructure.Models
{
    /// <summary>
    /// NestoAPI#332: efecto candidato a remesa (EfectoCandidatoDTO del API). Seleccionado se
    /// inicializa con la preselección del servidor y el usuario lo marca/desmarca; notifica
    /// para refrescar el importe seleccionado y el CanExecute de crear la remesa.
    /// </summary>
    public class EfectoCandidatoModel : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string Cliente { get; set; }
        public string Contacto { get; set; }
        public string Documento { get; set; }
        public string Efecto { get; set; }
        public DateTime Fecha { get; set; }
        public DateTime? Vencimiento { get; set; }
        public string Ccc { get; set; }
        public bool Preseleccionado { get; set; }
        public string Motivo { get; set; }

        /// <summary>
        /// Fallo 20/08/26 (NestoAPI): true = la retención es del gating de entrega (envío sin
        /// confirmar o incidentado) y el usuario puede forzar el efecto marcándolo — viaja al
        /// servidor en EfectosForzados. IBAN roto, estado bloqueado o DEVUELTO no son forzables.
        /// </summary>
        public bool Forzable { get; set; }

        // ImportePendiente y ClienteConNegativos notifican porque se actualizan EN SITIO cuando
        // el usuario liquida efectos en el Extracto de Cliente (EfectosLiquidadosEvent): el grid
        // debe reflejar el nuevo importe y quitar el naranja sin recargar toda la lista.
        private decimal _importePendiente;
        public decimal ImportePendiente
        {
            get => _importePendiente;
            set
            {
                if (_importePendiente != value)
                {
                    _importePendiente = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImportePendiente)));
                }
            }
        }

        private bool _clienteConNegativos;
        public bool ClienteConNegativos
        {
            get => _clienteConNegativos;
            set
            {
                if (_clienteConNegativos != value)
                {
                    _clienteConNegativos = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ClienteConNegativos)));
                }
            }
        }

        private bool _seleccionado;
        public bool Seleccionado
        {
            get => _seleccionado;
            set
            {
                if (_seleccionado != value)
                {
                    _seleccionado = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Seleccionado)));
                }
            }
        }

        // NestoAPI#896 (27/11/26): riesgo de devolución del recibo, solo para quien la API deja
        // verlo (Dirección e Informática). Llega DESPUÉS de cargar los candidatos, así que notifica.
        // Es solo informativo: no cambia ni la preselección ni lo que se remesa.
        private double? _probabilidadDevolucion;
        /// <summary>Probabilidad de devolución (0-1); null = sin dato. La columna «Riesgo» ordena por ella.</summary>
        public double? ProbabilidadDevolucion
        {
            get => _probabilidadDevolucion;
            set
            {
                if (_probabilidadDevolucion != value)
                {
                    _probabilidadDevolucion = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProbabilidadDevolucion)));
                }
            }
        }

        private string _riesgoTexto;
        /// <summary>El porcentaje con un decimal («23,7 %»); null sin dato.</summary>
        public string RiesgoTexto
        {
            get => _riesgoTexto;
            set
            {
                if (_riesgoTexto != value)
                {
                    _riesgoTexto = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RiesgoTexto)));
                }
            }
        }

        private int _nivelRiesgo;
        /// <summary>0 = sin color (sin dato o &lt; 5 %), 1 = 5-15 %, 2 = 15-30 %, 3 = ≥ 30 %.</summary>
        public int NivelRiesgo
        {
            get => _nivelRiesgo;
            set
            {
                if (_nivelRiesgo != value)
                {
                    _nivelRiesgo = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NivelRiesgo)));
                }
            }
        }

        private string _motivosRiesgo;
        /// <summary>Los motivos de la predicción, uno por línea (tooltip); null si no hay.</summary>
        public string MotivosRiesgo
        {
            get => _motivosRiesgo;
            set
            {
                if (_motivosRiesgo != value)
                {
                    _motivosRiesgo = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MotivosRiesgo)));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    /// <summary>
    /// NestoAPI#896 (27/11/26): respuesta de GET api/Remesas/ModeloImpagados/Candidatos
    /// (CandidatosRiesgoImpagosDTO del API). Solo llega con 200 a Dirección e Informática.
    /// </summary>
    public class CandidatosRiesgoImpagosModel
    {
        public DateTime Fecha { get; set; }
        public string VersionModelo { get; set; }
        public bool ConModelo { get; set; }
        public string Mensaje { get; set; }
        public TotalRiesgoImpagosModel Total { get; set; }
        public List<TramoRiesgoImpagosModel> Tramos { get; set; } = new List<TramoRiesgoImpagosModel>();
        public List<EfectoRiesgoImpagoModel> Efectos { get; set; } = new List<EfectoRiesgoImpagoModel>();
    }

    public class TotalRiesgoImpagosModel
    {
        public int Recibos { get; set; }
        public decimal Importe { get; set; }
        public double? DevolucionesEsperadas { get; set; }
        public decimal? ImporteEnRiesgo { get; set; }
    }

    public class TramoRiesgoImpagosModel
    {
        public string Tramo { get; set; }
        public int Recibos { get; set; }
        public decimal Importe { get; set; }
        public double DevolucionesEsperadas { get; set; }
    }

    public class EfectoRiesgoImpagoModel
    {
        public string Empresa { get; set; }
        public string Cliente { get; set; }
        public string Contacto { get; set; }
        public string Nombre { get; set; }
        public string Documento { get; set; }
        public string Efecto { get; set; }
        public decimal Importe { get; set; }
        public DateTime? FechaVto { get; set; }
        public int NumOrden { get; set; }
        public bool Preseleccionado { get; set; }
        public string MotivoRetencion { get; set; }
        public double? Probabilidad { get; set; }
        public double? ProbabilidadRegla { get; set; }
        public string Tramo { get; set; }
        public List<string> Motivos { get; set; } = new List<string>();
    }

    /// <summary>Respuesta del POST api/Remesas (NestoAPI#332).</summary>
    public class CrearRemesaResponseModel
    {
        public int NumeroRemesa { get; set; }
        public decimal Importe { get; set; }
        public int NumeroEfectos { get; set; }
    }

    /// <summary>
    /// NestoAPI#380 (ajuste 17/08/26, caso real 38429): movimiento negativo pendiente de un
    /// cliente, para que el aviso previo a crear la remesa muestre QUÉ es cada negativo (una
    /// señal de un curso que se facturará en septiembre no se liquida contra estos recibos;
    /// un abono de mercancía devuelta sí puede interesar) y el usuario decida con criterio.
    /// Se deserializa del ExtractoClienteDTO del API (GET ExtractosCliente?cliente=...).
    /// </summary>
    public class NegativoPendienteModel
    {
        public string Cliente { get; set; }
        public DateTime Fecha { get; set; }
        public string Concepto { get; set; }
        public decimal ImportePendiente { get; set; }
    }
}
