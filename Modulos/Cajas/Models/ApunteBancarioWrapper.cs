using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls.Ribbon.Primitives;

namespace Nesto.Modulos.Cajas.Models
{
    public class ApunteBancarioWrapper : BindableBase
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
                RaisePropertyChanged(nameof(Id));
            }
        }

        // Registro Principal de Movimientos
        public string CodigoRegistroPrincipal 
        {
            get => Model.CodigoRegistroPrincipal;
            set
            {
                Model.CodigoRegistroPrincipal = value;
                RaisePropertyChanged(nameof(CodigoRegistroPrincipal));
            }
        }
        public string ClaveOficinaOrigen 
        { 
            get => Model.ClaveOficinaOrigen;
            set
            {
                Model.ClaveOficinaOrigen = value;
                RaisePropertyChanged(nameof(ClaveOficinaOrigen));
            }
        }
        public DateTime FechaOperacion 
        {
            get => Model.FechaOperacion;
            set
            {
                Model.FechaOperacion = value;
                RaisePropertyChanged(nameof(FechaOperacion));
            }
        }
        public DateTime FechaValor 
        {
            get => Model.FechaValor;
            set
            {
                Model.FechaValor = value;
                RaisePropertyChanged(nameof(FechaValor));
            }
        }
        public string ConceptoComun 
        {
            get => Model.ConceptoComun;
            set
            {
                Model.ConceptoComun = value;
                RaisePropertyChanged(nameof(ConceptoComun));
            }
        }
        public string TextoConceptoComun 
        {
            get => Model.TextoConceptoComun;
            set
            {
                Model.TextoConceptoComun = value;
                RaisePropertyChanged(nameof(TextoConceptoComun));
            }
        }
        public string ConceptoPropio 
        {
            get => Model.ConceptoPropio;
            set
            {
                Model.ConceptoPropio = value;
                RaisePropertyChanged(nameof(ConceptoPropio));
            }
        }
        public string ClaveDebeOHaberMovimiento 
        {
            get => Model.ClaveDebeOHaberMovimiento;
            set
            {
                Model.ClaveDebeOHaberMovimiento = value;
                RaisePropertyChanged(nameof(ClaveDebeOHaberMovimiento));
            }
        }
        public decimal ImporteMovimiento 
        {
            get => Model.ImporteMovimiento;
            set
            {
                Model.ImporteMovimiento = value;
                RaisePropertyChanged(nameof(ImporteMovimiento));
            }
        }
        public string NumeroDocumento 
        {
            get => Model.NumeroDocumento;
            set
            {
                Model.NumeroDocumento = value;
                RaisePropertyChanged(nameof(NumeroDocumento));
            }
        }
        public string Referencia1
        {
            get => Model.Referencia1;
            set
            {
                Model.Referencia1 = value;
                RaisePropertyChanged(nameof(Referencia1));
            }
        }
        public string Referencia2
        {
            get => Model.Referencia2;
            set
            {
                Model.Referencia2 = value;
                RaisePropertyChanged(nameof(Referencia2));
            }
        }

        public EstadoPunteo EstadoPunteo
        {
            get => Model.EstadoPunteo;
            set
            {
                Model.EstadoPunteo = value;
                RaisePropertyChanged(nameof(EstadoPunteo));
            }
        }

        // Registros Complementarios de Concepto (Hasta un máximo de 5)
        public List<RegistroComplementarioConcepto> RegistrosConcepto 
        {
            get => Model.RegistrosConcepto;
            set
            {
                Model.RegistrosConcepto = value;
                RaisePropertyChanged(nameof(RegistrosConcepto));
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
