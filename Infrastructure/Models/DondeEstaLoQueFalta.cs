using CommunityToolkit.Mvvm.ComponentModel;
using Nesto.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Models
{
    /// <summary>
    /// «Dónde está lo que falta» (NestoAPI, 19/11/26, pedido 930942): GET api/PedidosVenta/{empresa}/{pedido}/DondeEstaLoQueFalta. Por cada producto
    /// del pedido que no está listo en Algete, una frase hecha (la escribe la API) y sus datos. Aquí solo se enseña.
    /// </summary>
    public class DondeEstaLoQueFaltaDTO
    {
        public string Empresa { get; set; }
        public int Pedido { get; set; }
        public string Mensaje { get; set; }
        public bool TodoEnAlgete { get; set; }
        public List<int> Pickings { get; set; } = new List<int>();
        public DateTime? FechaEntregaAgencia { get; set; }
        public List<ProductoQueFaltaDTO> Productos { get; set; } = new List<ProductoQueFaltaDTO>();
    }

    public class ProductoQueFaltaDTO
    {
        public string Producto { get; set; }
        public string Nombre { get; set; }
        public int Unidades { get; set; }
        /// <summary>«43818 MULTIVIT GLOW CREMA RICA (1 ud.): viene en la reposición 81463 de Alcobendas, recogida por Miguel…».</summary>
        public string Texto { get; set; }
        public List<SituacionQueFaltaDTO> Situaciones { get; set; } = new List<SituacionQueFaltaDTO>();
    }

    public class SituacionQueFaltaDTO
    {
        public string Origen { get; set; }
        public int Unidades { get; set; }
        public string Tienda { get; set; }
        public int? Traspaso { get; set; }
        public int? PedidoProveedor { get; set; }
        public DateTime? FechaPrevistaProveedor { get; set; }
        public DateTime? PuedeSalirEl { get; set; }
        public string Texto { get; set; }
    }

    /// <summary>
    /// Lo que se ve de «¿Dónde está lo que falta?» en el detalle del pedido y en «Pedidos con la fecha en riesgo»: una línea por producto o
    /// el mensaje («Todo está en Algete: tiene el picking 100120.»). Se pide solo cuando el usuario lo abre o elige un pedido: sin
    /// temporizadores. Si mientras tanto se pide otro pedido, la respuesta del anterior se descarta.
    /// </summary>
    public class DondeEstaLoQueFaltaVista : ObservableObject
    {
        public const string CARGANDO = "Buscando dónde está lo que falta…";
        public const string SIN_RESPUESTA = "No se ha podido saber dónde está lo que falta. Inténtelo de nuevo en un momento.";

        private readonly Func<IServicioDondeEstaLoQueFalta> _servicio;
        private int _version;
        private string _mensaje;
        private bool _cargando;

        public DondeEstaLoQueFaltaVista(Func<IServicioDondeEstaLoQueFalta> servicio)
        {
            _servicio = servicio;
        }

        /// <summary>Una línea por producto que falta (la frase de la API).</summary>
        public ObservableCollection<string> Lineas { get; } = new ObservableCollection<string>();

        public bool HayLineas => Lineas.Count > 0;

        public string Mensaje
        {
            get => _mensaje;
            private set
            {
                if (SetProperty(ref _mensaje, value))
                {
                    OnPropertyChanged(nameof(HayMensaje));
                }
            }
        }

        public bool HayMensaje => !string.IsNullOrWhiteSpace(Mensaje);

        public bool Cargando
        {
            get => _cargando;
            private set => SetProperty(ref _cargando, value);
        }

        /// <summary>De qué pedido es lo que se ve (null: nada).</summary>
        public int? Pedido { get; private set; }

        public void Limpiar()
        {
            _version++;
            Pedido = null;
            Cargando = false;
            Lineas.Clear();
            OnPropertyChanged(nameof(HayLineas));
            Mensaje = null;
        }

        /// <summary>Lo pide a la API y lo enseña. Nunca lanza: sin respuesta, <see cref="SIN_RESPUESTA"/>.</summary>
        public async Task MostrarAsync(string empresa, int pedido)
        {
            _version++;
            int version = _version;
            Pedido = pedido;
            Lineas.Clear();
            OnPropertyChanged(nameof(HayLineas));
            IServicioDondeEstaLoQueFalta servicio = _servicio?.Invoke();
            if (servicio == null || pedido <= 0)
            {
                Mensaje = SIN_RESPUESTA;
                return;
            }
            Mensaje = CARGANDO;
            Cargando = true;
            DondeEstaLoQueFaltaDTO resultado = null;
            try
            {
                resultado = await servicio.Leer(empresa, pedido).ConfigureAwait(true);
            }
            catch (Exception)
            {
                resultado = null;
            }
            if (version != _version)
            {
                return; // ya se ha pedido otro pedido (o se ha cerrado)
            }
            Cargando = false;
            Aplicar(resultado);
        }

        /// <summary>Pura (sin la API): pone lo que contesta la API.</summary>
        public void Aplicar(DondeEstaLoQueFaltaDTO resultado)
        {
            Lineas.Clear();
            if (resultado == null)
            {
                OnPropertyChanged(nameof(HayLineas));
                Mensaje = SIN_RESPUESTA;
                return;
            }
            foreach (string texto in (resultado.Productos ?? new List<ProductoQueFaltaDTO>())
                .Where(p => p != null && !string.IsNullOrWhiteSpace(p.Texto)).Select(p => p.Texto.Trim()))
            {
                Lineas.Add(texto);
            }
            OnPropertyChanged(nameof(HayLineas));
            Mensaje = string.IsNullOrWhiteSpace(resultado.Mensaje) ? null : resultado.Mensaje.Trim();
        }
    }
}
