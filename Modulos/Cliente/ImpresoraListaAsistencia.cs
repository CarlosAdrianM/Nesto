using Nesto.Modulos.Cliente.Models;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Nesto.Modulos.Cliente
{
    /// <summary>Sugerencia 1362: imprime la hoja para pasar lista (sustituible en los tests).</summary>
    public interface IImpresoraListaAsistencia
    {
        /// <summary>Enseña el diálogo de impresión de Windows e imprime. False si el usuario lo cancela.</summary>
        bool Imprimir(EventoModel evento, IEnumerable<InscripcionEventoModel> inscripciones);
    }

    /// <summary>
    /// La hoja con un FlowDocument y el PrintDialog de Windows (desde ahí se elige impresora o «Microsoft Print to PDF»). Qué
    /// columnas lleva y qué filas, <see cref="ListaAsistenciaImprimible"/>: aquí solo se dibuja.
    /// </summary>
    public class ImpresoraListaAsistencia : IImpresoraListaAsistencia
    {
        private const double MARGEN = 48; // medio pulgada larga
        private static readonly Brush BORDE = Brushes.Black;

        public bool Imprimir(EventoModel evento, IEnumerable<InscripcionEventoModel> inscripciones)
        {
            var dialogo = new PrintDialog();
            if (dialogo.ShowDialog() != true)
            {
                return false;
            }
            FlowDocument documento = Documento(evento, inscripciones);
            documento.PageWidth = dialogo.PrintableAreaWidth;
            documento.PageHeight = dialogo.PrintableAreaHeight;
            documento.ColumnWidth = dialogo.PrintableAreaWidth; // una sola columna de texto
            dialogo.PrintDocument(((IDocumentPaginatorSource)documento).DocumentPaginator, $"Lista de asistencia - {ListaAsistenciaImprimible.Titulo(evento)}");
            return true;
        }

        public static FlowDocument Documento(EventoModel evento, IEnumerable<InscripcionEventoModel> inscripciones)
        {
            var documento = new FlowDocument
            {
                PagePadding = new Thickness(MARGEN),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11,
                Background = Brushes.White
            };
            documento.Blocks.Add(new Paragraph(new Run(ListaAsistenciaImprimible.Titulo(evento)))
            {
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 2)
            });
            documento.Blocks.Add(new Paragraph(new Run(ListaAsistenciaImprimible.Subtitulo(evento))) { FontSize = 13, Margin = new Thickness(0) });
            documento.Blocks.Add(new Paragraph(new Run("Lista de asistencia · " + ListaAsistenciaImprimible.Resumen(inscripciones)))
            {
                Foreground = Brushes.DimGray,
                Margin = new Thickness(0, 2, 0, 10)
            });

            var tabla = new Table { CellSpacing = 0, BorderBrush = BORDE, BorderThickness = new Thickness(0.5) };
            foreach (ColumnaListaAsistencia columna in ListaAsistenciaImprimible.COLUMNAS)
            {
                tabla.Columns.Add(new TableColumn { Width = new GridLength(columna.Ancho, GridUnitType.Star) });
            }
            var grupo = new TableRowGroup();
            var cabecera = new TableRow { Background = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)), FontWeight = FontWeights.Bold };
            foreach (ColumnaListaAsistencia columna in ListaAsistenciaImprimible.COLUMNAS)
            {
                cabecera.Cells.Add(Celda(columna.Titulo));
            }
            grupo.Rows.Add(cabecera);
            foreach (string[] fila in ListaAsistenciaImprimible.Tabla(evento, inscripciones))
            {
                var tr = new TableRow();
                foreach (string valor in fila)
                {
                    tr.Cells.Add(Celda(valor, inscrito: true));
                }
                grupo.Rows.Add(tr);
            }
            tabla.RowGroups.Add(grupo);
            documento.Blocks.Add(tabla);
            return documento;
        }

        private static TableCell Celda(string texto, bool inscrito = false)
        {
            // Las filas de los inscritos, más altas: hay que poner la cruz de «Asiste» y firmar.
            var parrafo = new Paragraph(new Run(texto ?? string.Empty)) { Margin = new Thickness(0) };
            return new TableCell(parrafo)
            {
                BorderBrush = BORDE,
                BorderThickness = new Thickness(0.5),
                Padding = new Thickness(4, inscrito ? 7 : 3, 4, inscrito ? 7 : 3)
            };
        }
    }
}
