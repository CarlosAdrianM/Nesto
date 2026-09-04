using Newtonsoft.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cajas.Models
{
    public class ContabilidadWrapper : ObservableObject
    {
        public ContabilidadWrapper(ContabilidadDTO model)
        {
            Model = model;
        }

        public ContabilidadDTO Model { get; set; }

        public int Id 
        { 
            get => Model.Id;
            set 
            { 
                Model.Id = value;
                OnPropertyChanged(nameof(Id));
            } 
        }
        public string Empresa
        {
            get => Model.Empresa;
            set
            {
                Model.Empresa = value;
                OnPropertyChanged(nameof(Empresa));
            }
        }
        public string Cuenta 
        {
            get => Model.Cuenta;
            set
            {
                Model.Cuenta = value;
                OnPropertyChanged(nameof(Cuenta));
            }
        }
        public string Concepto 
        {
            get => Model.Concepto;
            set
            {
                Model.Concepto = value;
                OnPropertyChanged(nameof(Concepto));
            }
        }
        public decimal Debe 
        { 
            get => Model.Debe;
            set
            {
                Model.Debe = value;
                OnPropertyChanged(nameof(Debe));
            }
        }
        public decimal Haber 
        { 
            get => Model.Haber; 
            set
            {
                Model.Haber = value;
                OnPropertyChanged(nameof(Haber));
            }
        }
        public decimal Importe => Debe - Haber;
        public DateTime Fecha 
        {
            get => Model.Fecha; 
            set
            {
                Model.Fecha = value;
                OnPropertyChanged(nameof(Fecha));
            }
        }
        public string Documento 
        {
            get => Model.Documento;
            set
            {
                Model.Documento = value;
                OnPropertyChanged(nameof(Documento));
            }
        }
        public string Delegacion
        {
            get => Model.Delegacion;
            set
            {
                Model.Delegacion = value;
                OnPropertyChanged(nameof(Delegacion));
            }
        }
        public int Asiento 
        {
            get => Model.Asiento;
            set
            {
                Model.Asiento = value;
                OnPropertyChanged(nameof(Asiento));
            }
        }
        public string Diario
        {
            get => Model.Diario;
            set
            {
                Model.Diario = value;
                OnPropertyChanged(nameof(Diario));
            }
        }
        public EstadoPunteo EstadoPunteo
        {
            get => Model.EstadoPunteo;
            set
            {
                Model.EstadoPunteo = value;
                OnPropertyChanged(nameof(EstadoPunteo));
            }
        }
        public string Usuario
        {
            get => Model.Usuario;
            set
            {
                Model.Usuario = value;
                OnPropertyChanged(nameof(Usuario));
            }
        }
        private bool _visible = true;
        public bool Visible
        {
            get { return _visible; }
            set => SetProperty(ref _visible, value);
        }
    }
}
