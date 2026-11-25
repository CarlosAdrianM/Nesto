using CommunityToolkit.Mvvm.ComponentModel;
using Nesto.Modulos.Cajas.Models;
using System;

namespace Nesto.Modulos.Cajas.ViewModels
{
    /// <summary>Proyectos (gemela de NestoAPI#901): una fila del reparto de un apunte (proyecto, importe y comentario).</summary>
    public class FilaRepartoProyecto : ObservableObject
    {
        /// <summary>Se avisa al panel de reparto para recalcular lo pendiente en vivo.</summary>
        internal event EventHandler? Cambiada;

        private int? _proyectoId;
        public int? ProyectoId
        {
            get => _proyectoId;
            set
            {
                if (SetProperty(ref _proyectoId, value))
                {
                    Cambiada?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private decimal? _importe;
        public decimal? Importe
        {
            get => _importe;
            set
            {
                if (SetProperty(ref _importe, value))
                {
                    Cambiada?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private string? _comentario;
        public string? Comentario
        {
            get => _comentario;
            set
            {
                if (SetProperty(ref _comentario, value))
                {
                    Cambiada?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>El proyecto está cerrado: la API no deja cambiar ni quitar su parte (se enseña, pero no se toca).</summary>
        public bool ProyectoCerrado { get; init; }

        public ParteReparto Parte => new ParteReparto(ProyectoId, Importe, Comentario);
    }
}
