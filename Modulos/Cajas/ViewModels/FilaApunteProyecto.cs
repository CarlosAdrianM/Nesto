using CommunityToolkit.Mvvm.ComponentModel;
using Nesto.Modulos.Cajas.Models;
using System;

namespace Nesto.Modulos.Cajas.ViewModels
{
    /// <summary>
    /// Proyectos (gemela de NestoAPI#901): un apunte en la rejilla de «Apuntes por imputar». Se actualiza en su sitio tras guardar
    /// su reparto (así la rejilla no pierde la selección).
    /// </summary>
    public class FilaApunteProyecto : ObservableObject
    {
        public FilaApunteProyecto(ApunteProyectoModel apunte)
        {
            Apunte = apunte ?? throw new ArgumentNullException(nameof(apunte));
        }

        public ApunteProyectoModel Apunte { get; private set; }

        public void Actualizar(ApunteProyectoModel apunte)
        {
            if (apunte == null)
            {
                return;
            }
            Apunte = apunte;
            OnPropertyChanged(string.Empty);
        }

        public int NumeroOrden => Apunte.NumeroOrden;
        public DateTime Fecha => Apunte.Fecha;
        public string CuentaYNombre => Apunte.CuentaYNombre;
        public string? Concepto => Apunte.Concepto?.Trim();
        public string? Documento => Apunte.Documento?.Trim();
        public string TerceroParaMostrar => Apunte.TerceroParaMostrar;
        public string NaturalezaParaMostrar => Apunte.NaturalezaParaMostrar;
        public decimal ImporteApunte => Apunte.ImporteApunte;
        public decimal Imputado => Apunte.Imputado;
        public decimal Pendiente => Apunte.Pendiente;
        public string ProyectosParaMostrar => Apunte.ProyectosParaMostrar;
        public bool EsAbono => Apunte.EsAbono;
    }
}
