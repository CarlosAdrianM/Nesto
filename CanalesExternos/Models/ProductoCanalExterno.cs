using Nesto.Infrastructure.Shared;
using Nesto.Modules.Producto.Models;
using Prism.Mvvm;

namespace Nesto.Modulos.CanalesExternos.Models
{
    public class ProductoCanalExterno : BindableBase
    {
        private string _productoId;
        public string ProductoId { 
            get => _productoId;
            set
            {
                if(SetProperty(ref _productoId, value))
                {
                    IsDirty = true;
                }
            }
        }
        private string? _nombre;
        public string? Nombre
        {
            get => _nombre;
            set
            {
                if(SetProperty(ref _nombre, value))
                {
                    IsDirty = true;
                }
            }
        }
        private string _descripcionBreve;
        public string DescripcionBreve {
            get => _descripcionBreve;
            set 
            {
                if(SetProperty(ref _descripcionBreve, value))
                {
                    IsDirty = true;
                }
            }

        }
        private string _descripcionCompleta;
        public string DescripcionCompleta {
            get => _descripcionCompleta;
            set
            {
                if(SetProperty(ref _descripcionCompleta, value))
                {
                    IsDirty = true;
                }
            }
        }
        private decimal? _pvpIvaIncluido;
        public decimal? PvpIvaIncluido {
            get => _pvpIvaIncluido;
            set
            {
                if(SetProperty(ref _pvpIvaIncluido, value))
                {
                    IsDirty = true;
                    RaisePropertyChanged(nameof(ModoPrecio));
                    RaisePropertyChanged(nameof(EsPrecioFijo));
                    RaisePropertyChanged(nameof(PrecioPublicoTexto));
                }
            }
        }

        public const string TEXTO_DESCUENTO_POR_DEFECTO = "Descuento por defecto (30 %)";
        public const string TEXTO_MISMO_QUE_PROFESIONAL = "Mismo precio que el profesional";

        /// <summary>
        /// Nesto#453: modo del precio público, derivado de PvpIvaIncluido (positivo / NULL / -1).
        /// Al cambiarlo se escribe el valor que corresponde en PvpIvaIncluido, que es lo que viaja en el PUT.
        /// </summary>
        public ModoPrecioPublico ModoPrecio
        {
            get => ModoDe(_pvpIvaIncluido);
            set
            {
                if (value == ModoPrecio)
                {
                    return;
                }
                switch (value)
                {
                    case ModoPrecioPublico.DescuentoPorDefecto:
                        PvpIvaIncluido = null;
                        break;
                    case ModoPrecioPublico.MismoQueProfesional:
                        PvpIvaIncluido = Constantes.Productos.PVP_IVA_MISMO_QUE_PROFESIONAL;
                        break;
                    default:
                        // Al pasar a precio fijo se propone el público que calcula hoy la API, para no
                        // dejar la caja vacía (0 se serviría como "sin precio fijo").
                        decimal propuesto = ProductoCompleto?.PrecioPublicoFinal ?? 0M;
                        PvpIvaIncluido = propuesto > 0 ? propuesto : 0M;
                        break;
                }
            }
        }

        public bool EsPrecioFijo => ModoPrecio == ModoPrecioPublico.PrecioFijo;

        /// <summary>Nesto#453: lo que se enseña en la columna "Precio Público" de Revisar (nunca "-1,00 €").</summary>
        public string PrecioPublicoTexto => ModoPrecio switch
        {
            ModoPrecioPublico.DescuentoPorDefecto => TEXTO_DESCUENTO_POR_DEFECTO,
            ModoPrecioPublico.MismoQueProfesional => TEXTO_MISMO_QUE_PROFESIONAL,
            _ => _pvpIvaIncluido.Value.ToString("C")
        };

        public static ModoPrecioPublico ModoDe(decimal? pvpIvaIncluido)
        {
            if (!pvpIvaIncluido.HasValue)
            {
                return ModoPrecioPublico.DescuentoPorDefecto;
            }
            return pvpIvaIncluido.Value == Constantes.Productos.PVP_IVA_MISMO_QUE_PROFESIONAL
                ? ModoPrecioPublico.MismoQueProfesional
                : ModoPrecioPublico.PrecioFijo;
        }
        private bool _vistoBueno;
        public bool VistoBueno {
            get => _vistoBueno;
            set
            {
                if(SetProperty(ref _vistoBueno, value))
                {
                    IsDirty = true;
                }
            }
        }
        private bool _isDirty;
        public bool IsDirty
        {
            get => _isDirty;
            set => SetProperty(ref _isDirty, value);
        }
        public ProductoModel ProductoCompleto { get; set; }
    }
}
