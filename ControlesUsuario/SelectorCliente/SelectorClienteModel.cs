using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ControlesUsuario.Models
{
    public class ClienteDTO : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public string empresa { get; set; }
        private string _cliente;
        public string cliente {
            get {
                return _cliente;
            }
            set {
                _cliente = value;
                OnPropertyChanged("cliente");
            }
        }
        private string _contacto;
        public string contacto {
            get {
                return _contacto;
            }
            set {
                _contacto = value;
                OnPropertyChanged("contacto");
            }
        }
        public string nombre { get; set; }
        public string direccion { get; set; }
        public string poblacion { get; set; }
        public string telefono { get; set; }
        public string codigoPostal { get; set; }
        public string provincia { get; set; }
        public string comentarios { get; set; }
        public int estado { get; set; }
        public string vendedor { get; set; }
        public string cifNif { get; set; }
        public string poblacionConCodigoPostal {
            get
            {
                return string.Format("{0} {1} ({2})", codigoPostal, poblacion, provincia);
            } 
        }
        public List<VendedorGrupoProductoDTO> VendedoresGrupoProducto { get; set; }

        private void OnPropertyChanged([CallerMemberName] String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

    }

    public class VendedorGrupoProductoDTO
    {
        public string GrupoProducto { get; set; }
        public string Vendedor { get; set; }
    }
}
