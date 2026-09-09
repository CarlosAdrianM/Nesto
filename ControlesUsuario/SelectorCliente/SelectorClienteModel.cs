using Nesto.Infrastructure.Contracts;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ControlesUsuario.Models
{
    public class ClienteDTO : INotifyPropertyChanged, IFiltrableItem
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public string empresa { get; set; }
        private string _cliente;
        public string cliente {
            get 
            {
                return _cliente;
            }
            set {
                _cliente = value;
                OnPropertyChanged(nameof(cliente));
            }
        }
        private string _contacto;
        public string contacto {
            get {
                return _contacto;
            }
            set {
                _contacto = value;
                OnPropertyChanged(nameof(contacto));
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
        // NestoAPI#464: lo que sabe la ficha y si la pantalla de rapport tiene que preguntarlo. La
        // regla (hoy, código postal de Madrid) la calcula la API; aquí solo se lee el resultado.
        public byte? empleados { get; set; }
        public DateTime? empleadosFecha { get; set; }
        public bool preguntarEmpleados { get; set; }
        public string poblacionConCodigoPostal {
            get
            {
                return string.Format("{0} {1} ({2})", codigoPostal?.Trim(), poblacion?.Trim(), provincia?.Trim());
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

        public bool Contains(string filtro)
        {
            return (nombre != null && nombre.ToLower().Contains(filtro)) ||
                   (direccion != null && direccion.ToLower().Contains(filtro)) ||
                   (telefono != null && telefono.ToLower().Contains(filtro)) ||
                   (poblacion != null && poblacion.ToLower().Contains(filtro));
        }
    }

    public class VendedorGrupoProductoDTO
    {
        public string GrupoProducto { get; set; }
        public string Vendedor { get; set; }
    }
}
