using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlesUsuario.Services;
using Nesto.Infrastructure.Contracts;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modules.Producto.ViewModels
{
    /// <summary>
    /// Carlos 20/11/26: la pestaña «No recogidas» de la ventana «Recogidas solicitadas». El repartidor marca en Ariadna «No se ha
    /// recogido» y aquí Almacén decide, por fila (cliente, dirección, motivo del repartidor, quién y cuándo):
    /// <list type="bullet">
    /// <item>«Volver a mandar» (POST api/RepartoPropio/Recogidas/{pedido}/Reprogramar, fecha opcional: sin ella, el siguiente día de
    /// reparto del cliente): la misma nota vuelve al reparto.</item>
    /// <item>«Cerrar del todo» (POST …/Anular con motivo obligatorio, el mismo paso que «Anular recogida» de Agencias).</item>
    /// </list>
    /// Se lee al abrir y con «Actualizar» (sin sondeo). Con un servidor que aún no lo tiene se dice y la ventana sigue funcionando.
    /// </summary>
    public class RecogidasNoRecogidasViewModel : ObservableObject
    {
        public const string TITULO_VOLVER_A_MANDAR = "Volver a mandar";
        public const string ETIQUETA = "No recogidas";
        public const string MENSAJE_SIN_GRUPO = "Volver a mandar o cerrar las recogidas no recogidas es cosa de Almacén, Administración, Dirección e Informática.";

        private readonly IServicioRecogidasReparto _servicio;
        private readonly IServicioDialogos _dialogos;

        public RecogidasNoRecogidasViewModel(IServicioRecogidasReparto servicio, IServicioDialogos dialogos, bool puedeGestionar)
        {
            _servicio = servicio;
            _dialogos = dialogos;
            PuedeGestionar = puedeGestionar;
            CargarCommand = new AsyncRelayCommand(CargarAsync, () => !EstaOcupado && _servicio != null);
            VolverAMandarCommand = new AsyncRelayCommand(VolverAMandarAsync, () => PuedeActuar);
            CerrarDelTodoCommand = new AsyncRelayCommand(CerrarDelTodoAsync, () => PuedeActuar);
        }

        public bool PuedeGestionar { get; }

        public IAsyncRelayCommand CargarCommand { get; }
        public IAsyncRelayCommand VolverAMandarCommand { get; }
        public IAsyncRelayCommand CerrarDelTodoCommand { get; }

        public ObservableCollection<RecogidaNoRecogidaModel> Lista { get; } = new ObservableCollection<RecogidaNoRecogidaModel>();

        private RecogidaNoRecogidaModel _seleccionada;
        public RecogidaNoRecogidaModel Seleccionada
        {
            get => _seleccionada;
            set
            {
                if (SetProperty(ref _seleccionada, value))
                {
                    NotificarComandos();
                }
            }
        }

        private DateTime? _fechaVolverAMandar;
        /// <summary>Opcional: el día en que se vuelve a mandar. Vacío: el siguiente día de reparto del cliente (lo decide la API).</summary>
        public DateTime? FechaVolverAMandar { get => _fechaVolverAMandar; set => SetProperty(ref _fechaVolverAMandar, value); }

        private bool _estaOcupado;
        public bool EstaOcupado
        {
            get => _estaOcupado;
            private set
            {
                if (SetProperty(ref _estaOcupado, value))
                {
                    NotificarComandos();
                }
            }
        }

        private bool _disponible = true;
        /// <summary>False si el servidor aún no lo tiene (o falta el script): nada se puede hacer.</summary>
        public bool Disponible
        {
            get => _disponible;
            private set
            {
                if (SetProperty(ref _disponible, value))
                {
                    NotificarComandos();
                }
            }
        }

        private string _aviso;
        /// <summary>El aviso fijo: servidor sin actualizar, sin permiso o sin grupo.</summary>
        public string Aviso
        {
            get => _aviso;
            private set
            {
                if (SetProperty(ref _aviso, value))
                {
                    OnPropertyChanged(nameof(HayAviso));
                }
            }
        }

        public bool HayAviso => !string.IsNullOrWhiteSpace(Aviso);

        private string _mensaje;
        public string Mensaje { get => _mensaje; private set => SetProperty(ref _mensaje, value); }

        /// <summary>La cabecera de la pestaña: «No recogidas» o «No recogidas (3)».</summary>
        public string Cabecera => Lista.Count > 0 ? $"{ETIQUETA} ({Lista.Count})" : ETIQUETA;

        public string TextoLista => Lista.Count switch
        {
            0 => "No hay recogidas no recogidas pendientes de decidir.",
            1 => "1 recogida no recogida pendiente de decidir.",
            _ => $"{Lista.Count} recogidas no recogidas pendientes de decidir."
        };

        private bool PuedeActuar => PuedeGestionar && Disponible && !EstaOcupado && Seleccionada != null && _servicio != null;

        /// <summary>Lee la lista (al abrir y con «Actualizar»). Nunca lanza.</summary>
        public async Task CargarAsync()
        {
            if (_servicio == null)
            {
                return;
            }
            EstaOcupado = true;
            try
            {
                List<RecogidaNoRecogidaModel> leidas = await _servicio.LeerNoRecogidas().ConfigureAwait(true) ?? new List<RecogidaNoRecogidaModel>();
                int? elegir = Seleccionada?.Pedido;
                Lista.Clear();
                foreach (RecogidaNoRecogidaModel recogida in leidas.Where(r => r != null))
                {
                    Lista.Add(recogida);
                }
                Seleccionada = Lista.FirstOrDefault(r => r.Pedido == elegir) ?? Lista.FirstOrDefault();
                Disponible = true;
                Aviso = PuedeGestionar ? null : MENSAJE_SIN_GRUPO;
            }
            catch (ApiRepartoException ex) when (ex.ServidorSinActualizar || ex.CodigoHttp == 403)
            {
                Disponible = !ex.ServidorSinActualizar;
                Lista.Clear();
                Seleccionada = null;
                Aviso = ex.Message;
            }
            catch (Exception ex)
            {
                Mensaje = "No se han podido leer las recogidas no recogidas: " + ex.Message;
            }
            finally
            {
                EstaOcupado = false;
                OnPropertyChanged(nameof(Cabecera));
                OnPropertyChanged(nameof(TextoLista));
            }
        }

        /// <summary>PURA. La pregunta antes de volver a mandarla.</summary>
        internal static string PreguntaVolverAMandar(RecogidaNoRecogidaModel recogida, DateTime? fecha)
        {
            string cuando = fecha.HasValue ? $"el {fecha.Value:dd/MM/yyyy}" : "el siguiente día de reparto del cliente";
            return $"La recogida {recogida.Codigo} de {recogida.ClienteTexto} vuelve al reparto {cuando}.\n\n¿La vuelve a mandar?";
        }

        internal async Task VolverAMandarAsync()
        {
            RecogidaNoRecogidaModel recogida = Seleccionada;
            if (recogida == null || _dialogos == null || _servicio == null)
            {
                return;
            }
            Mensaje = null;
            DateTime? fecha = FechaVolverAMandar?.Date;
            if (fecha.HasValue && fecha.Value < DateTime.Today)
            {
                Mensaje = "La fecha no puede ser anterior a hoy.";
                return;
            }
            if (!_dialogos.ShowConfirmationAnswer(TITULO_VOLVER_A_MANDAR, PreguntaVolverAMandar(recogida, fecha)))
            {
                return;
            }
            EstaOcupado = true;
            try
            {
                ReprogramacionRecogidaModel resultado = await _servicio.Reprogramar(recogida.Pedido, fecha).ConfigureAwait(true);
                EstaOcupado = false;
                Mensaje = !string.IsNullOrWhiteSpace(resultado?.Mensaje)
                    ? resultado.Mensaje.Trim()
                    : $"La recogida {recogida.Codigo} vuelve al reparto" + (resultado?.FechaEntrega.HasValue == true ? $" el {resultado.FechaEntrega.Value:dd/MM/yyyy}." : ".");
                FechaVolverAMandar = null;
            }
            catch (Exception ex)
            {
                EstaOcupado = false;
                if (TratarError(ex))
                {
                    return;
                }
            }
            await CargarAsync().ConfigureAwait(true);
        }

        internal async Task CerrarDelTodoAsync()
        {
            RecogidaNoRecogidaModel recogida = Seleccionada;
            if (recogida == null || _dialogos == null || _servicio == null)
            {
                return;
            }
            Mensaje = null;
            // El anulador pide el motivo, confirma y enseña el error de usted (los diálogos son modales: no hace falta «ocupado»)
            AnulacionRecogidaModel anulada = await new AnuladorRecogidas(_servicio, _dialogos)
                .Anular(recogida.Pedido, recogida.EnvioId, recogida.Nombre).ConfigureAwait(true);
            if (anulada == null)
            {
                return; // cancelado o error (el anulador ya lo ha dicho)
            }
            Mensaje = string.IsNullOrWhiteSpace(anulada.Mensaje) ? $"La recogida {recogida.Codigo} queda cerrada del todo." : anulada.Mensaje.Trim();
            await CargarAsync().ConfigureAwait(true);
        }

        /// <summary>El error de una acción. True si no hay que releer (servidor sin actualizar o error que no cambia nada); con 404/409 se relee.</summary>
        private bool TratarError(Exception ex)
        {
            if (ex is ApiRepartoException api)
            {
                _dialogos?.ShowError(api.Message);
                Mensaje = api.Message;
                if (api.ServidorSinActualizar)
                {
                    Disponible = false;
                    Aviso = api.Message;
                    return true;
                }
                return !(api.CodigoHttp == 404 || api.CodigoHttp == 409);
            }
            Mensaje = "No se ha podido volver a mandar la recogida: " + ex.Message;
            _dialogos?.ShowError(Mensaje);
            return true;
        }

        private void NotificarComandos()
        {
            CargarCommand?.NotifyCanExecuteChanged();
            VolverAMandarCommand?.NotifyCanExecuteChanged();
            CerrarDelTodoCommand?.NotifyCanExecuteChanged();
        }
    }
}
