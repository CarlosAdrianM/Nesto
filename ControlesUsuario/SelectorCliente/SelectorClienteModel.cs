﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

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

        private void OnPropertyChanged([CallerMemberName] String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

    }
}