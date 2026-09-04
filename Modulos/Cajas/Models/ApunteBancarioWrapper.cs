using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;

namespace Nesto.Modulos.Cajas.Models
{
    public class ApunteBancarioWrapper : ObservableObject
    {
        public ApunteBancarioWrapper(ApunteBancarioDTO model)
        {
            Model = model;
        }

        public ApunteBancarioDTO Model { get; set; }

        public int Id 
        {
            get => Model.Id;
            set
            {
                Model.Id = value;
                OnPropertyChanged(nameof(Id));
            }
        }

        // Registro Principal de Movimientos
        public string CodigoRegistroPrincipal 
        {
            get => Model.CodigoRegistroPrincipal;
            set
            {
                Model.CodigoRegistroPrincipal = value;
                OnPropertyChanged(nameof(CodigoRegistroPrincipal));
            }
        }
        public string ClaveOficinaOrigen 
        { 
            get => Model.ClaveOficinaOrigen;
            set
            {
                Model.ClaveOficinaOrigen = value;
                OnPropertyChanged(nameof(ClaveOficinaOrigen));
            }
        }
        public DateTime FechaOperacion 
        {
            get => Model.FechaOperacion;
            set
            {
                Model.FechaOperacion = value;
                OnPropertyChanged(nameof(FechaOperacion));
            }
        }
        public DateTime FechaValor 
        {
            get => Model.FechaValor;
            set
            {
                Model.FechaValor = value;
                OnPropertyChanged(nameof(FechaValor));
            }
        }
        public string ConceptoComun 
        {
            get => Model.ConceptoComun;
            set
            {
                Model.ConceptoComun = value;
                OnPropertyChanged(nameof(ConceptoComun));
            }
        }
        public string TextoConceptoComun 
        {
            get => Model.TextoConceptoComun;
            set
            {
                Model.TextoConceptoComun = value;
                OnPropertyChanged(nameof(TextoConceptoComun));
            }
        }
        public string ConceptoPropio 
        {
            get => Model.ConceptoPropio;
            set
            {
                Model.ConceptoPropio = value;
                OnPropertyChanged(nameof(ConceptoPropio));
            }
        }
        public string ClaveDebeOHaberMovimiento 
        {
            get => Model.ClaveDebeOHaberMovimiento;
            set
            {
                Model.ClaveDebeOHaberMovimiento = value;
                OnPropertyChanged(nameof(ClaveDebeOHaberMovimiento));
            }
        }
        public decimal ImporteMovimiento 
        {
            get => Model.ImporteMovimiento;
            set
            {
                Model.ImporteMovimiento = value;
                OnPropertyChanged(nameof(ImporteMovimiento));
            }
        }
        public string NumeroDocumento 
        {
            get => Model.NumeroDocumento;
            set
            {
                Model.NumeroDocumento = value;
                OnPropertyChanged(nameof(NumeroDocumento));
            }
        }
        public string Referencia1
        {
            get => Model.Referencia1;
            set
            {
                Model.Referencia1 = value;
                OnPropertyChanged(nameof(Referencia1));
            }
        }
        public string Referencia2
        {
            get => Model.Referencia2;
            set
            {
                Model.Referencia2 = value;
                OnPropertyChanged(nameof(Referencia2));
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

        // Registros Complementarios de Concepto (Hasta un máximo de 5)
        public List<RegistroComplementarioConcepto> RegistrosConcepto 
        {
            get => Model.RegistrosConcepto;
            set
            {
                Model.RegistrosConcepto = value;
                OnPropertyChanged(nameof(RegistrosConcepto));
            }
        }

        private bool _visible = true; 
        public bool Visible
        {
            get { return _visible; }
            set => SetProperty(ref _visible, value);
        }


        // Registro Complementario de Información de Equivalencia de Importe (Opcional)
        public RegistroComplementarioEquivalencia ImporteEquivalencia { get; set; }

    }
}
