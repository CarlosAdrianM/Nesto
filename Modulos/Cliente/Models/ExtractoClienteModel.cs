using System;
using System.ComponentModel;

namespace Nesto.Modulos.Cliente.Models
{
    /// <summary>
    /// Nesto#419: movimiento del extracto de cliente (ExtractoClienteDTO de NestoAPI, que ya
    /// viene con nombres en camelCase — Newtonsoft es case-insensitive). Seleccionado es
    /// estado de UI (checkbox del grid para elegir los dos movimientos a liquidar) y notifica
    /// para que el ViewModel refresque el CanExecute de Liquidar.
    /// </summary>
    public class ExtractoClienteModel : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string Empresa { get; set; }
        public int Asiento { get; set; }
        public string Cliente { get; set; }
        public string Contacto { get; set; }
        public DateTime Fecha { get; set; }
        public string Tipo { get; set; }
        public string Documento { get; set; }
        public string Efecto { get; set; }
        public string Concepto { get; set; }
        public decimal Importe { get; set; }
        public decimal ImportePendiente { get; set; }
        public string Vendedor { get; set; }
        public DateTime? Vencimiento { get; set; }
        public string Ccc { get; set; }
        public string Ruta { get; set; }
        public string Estado { get; set; }
        public string FormaPago { get; set; }
        /// <summary>Nesto#478 / NestoAPI#492: el nº de documento es una factura existente; se puede abrir desde la fila.</summary>
        public bool TieneFactura { get; set; }

        private SenalEventoModel _senal;
        /// <summary>NestoAPI#591: si este apunte a favor es la señal de un evento (estado de UI, viene de api/Eventos/Senales/Cliente).</summary>
        public SenalEventoModel Senal
        {
            get => _senal;
            set
            {
                if (_senal != value)
                {
                    _senal = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Senal)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SenalTexto)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EsSenal)));
                }
            }
        }

        public bool EsSenal => Senal != null;

        /// <summary>«Señal: Masterclass Cloasma 13/10» o vacío.</summary>
        public string SenalTexto => Senal?.TextoExtracto;

        private bool _seleccionado;
        public bool Seleccionado
        {
            get => _seleccionado;
            set
            {
                // NestoAPI#876: un movimiento de otra ficha equivalente no se marca para liquidar
                bool nuevo = value && !EsDeOtraFicha;
                if (_seleccionado != nuevo)
                {
                    _seleccionado = nuevo;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Seleccionado)));
                }
            }
        }

        private bool _esDeOtraFicha;
        /// <summary>
        /// NestoAPI#876 (27/11/26, estado de UI): con «Incluir fichas equivalentes», el movimiento es de otra ficha (su número en
        /// <see cref="Cliente"/>): solo se consulta; ni se liquida ni se marca como señal desde aquí.
        /// </summary>
        public bool EsDeOtraFicha
        {
            get => _esDeOtraFicha;
            set
            {
                if (_esDeOtraFicha != value)
                {
                    _esDeOtraFicha = value;
                    if (value)
                    {
                        Seleccionado = false;
                    }
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EsDeOtraFicha)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SePuedeTocar)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MotivoSoloLectura)));
                }
            }
        }

        public bool SePuedeTocar => !EsDeOtraFicha;

        /// <summary>Por qué la fila no se puede tocar (null si se puede).</summary>
        public string MotivoSoloLectura => EsDeOtraFicha ? ReglasPantallaEquivalentes.MOTIVO_SOLO_LECTURA : null;

        public event PropertyChangedEventHandler PropertyChanged;
    }

    /// <summary>Respuesta del POST ExtractosCliente/Liquidar (NestoAPI#333).</summary>
    public class ResultadoLiquidacionModel
    {
        public bool Exito { get; set; }
        public decimal ImportePdteOrigen { get; set; }
        public decimal ImportePdteDestino { get; set; }
    }
}
