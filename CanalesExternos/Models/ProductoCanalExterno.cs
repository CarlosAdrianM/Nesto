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
                }
            }
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
