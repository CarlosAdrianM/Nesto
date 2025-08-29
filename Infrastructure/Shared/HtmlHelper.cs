using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Navigation;

public static class HtmlHelper
{
    /// <summary>
    /// Convierte HTML básico a FlowDocument
    /// </summary>
    /// <param name="html">HTML a convertir</param>
    /// <param name="hyperlinkHandler">Manejador opcional para los eventos de clic en enlaces. 
    /// Si es null, se usa el comportamiento por defecto (abrir en navegador)</param>
    /// <returns>FlowDocument con el contenido del HTML</returns>
    public static FlowDocument ConvertHtmlToFlowDocument(string html, RequestNavigateEventHandler hyperlinkHandler = null)
    {
        var converter = new HtmlConverter(hyperlinkHandler);
        return converter.HtmlBasicoAFlowDocument(html);
    }

    public static FlowDocument PlainTextToFlowDocument(string texto)
    {
        var doc = new FlowDocument();
        if (string.IsNullOrEmpty(texto))
        {
            return doc;
        }

        var parrafo = new Paragraph();
        var regex = new Regex(@"(https?://[^\s]+)", RegexOptions.Compiled);

        int lastIndex = 0;
        foreach (Match match in regex.Matches(texto))
        {
            // Añade el texto antes del enlace, respetando saltos de línea
            if (match.Index > lastIndex)
            {
                string fragmento = texto[lastIndex..match.Index];
                foreach (var linea in fragmento.Split('\n'))
                {
                    parrafo.Inlines.Add(new Run(linea));
                    parrafo.Inlines.Add(new LineBreak());
                }
            }

            // Añade el enlace
            var link = new Hyperlink(new Run(match.Value))
            {
                NavigateUri = new Uri(match.Value)
            };
            link.RequestNavigate += DefaultHyperlinkHandler;
            parrafo.Inlines.Add(link);

            lastIndex = match.Index + match.Length;
        }

        // Añade el texto restante, respetando saltos de línea
        if (texto.Length > lastIndex)
        {
            string fragmento = texto[lastIndex..];
            foreach (var linea in fragmento.Split('\n'))
            {
                parrafo.Inlines.Add(new Run(linea));
                parrafo.Inlines.Add(new LineBreak());
            }
        }

        doc.Blocks.Add(parrafo);
        return doc;
    }

    /// <summary>
    /// Manejador por defecto que abre enlaces en el navegador
    /// </summary>
    private static void DefaultHyperlinkHandler(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            _ = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri)
            {
                UseShellExecute = true
            });
            e.Handled = true;
        }
        catch (Exception ex)
        {
            // En caso de error, no hacemos nada (el enlace simplemente no se abre)
            // Podrías loggear el error aquí si tu aplicación tiene logging
            System.Diagnostics.Debug.WriteLine($"Error al abrir enlace {e.Uri}: {ex.Message}");
        }
    }

    // Clase interna para manejar la conversión
    private class HtmlConverter
    {
        private readonly RequestNavigateEventHandler _hyperlinkHandler;

        public HtmlConverter(RequestNavigateEventHandler hyperlinkHandler)
        {
            // Si no se proporciona manejador, usamos el por defecto
            _hyperlinkHandler = hyperlinkHandler ?? DefaultHyperlinkHandler;
        }

        public FlowDocument HtmlBasicoAFlowDocument(string html)
        {
            var doc = new FlowDocument();
            if (string.IsNullOrWhiteSpace(html))
            {
                return doc;
            }

            try
            {
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml($"<div>{html}</div>"); // Envolvemos en un div para asegurar estructura válida

                var rootNode = htmlDoc.DocumentNode.FirstChild;
                if (rootNode != null)
                {
                    ProcessNodes(rootNode.ChildNodes, doc.Blocks);
                }
            }
            catch (Exception)
            {
                // En caso de error, añadimos el texto como párrafo simple
                doc.Blocks.Add(new Paragraph(new Run(html)));
            }

            return doc;
        }

        private void ProcessNodes(HtmlNodeCollection nodes, BlockCollection blocks)
        {
            foreach (var node in nodes)
            {
                var block = ProcessNode(node);
                if (block != null)
                {
                    blocks.Add(block);
                }
            }
        }

        private Block ProcessNode(HtmlNode node)
        {
            if (node == null)
            {
                return null;
            }

            switch (node.NodeType)
            {
                case HtmlNodeType.Text:
                    var text = HtmlEntity.DeEntitize(node.InnerText?.Trim());
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return new Paragraph(new Run(text));
                    }
                    break;

                case HtmlNodeType.Element:
                    return ProcessElement(node);
            }

            return null;
        }

        private Block ProcessElement(HtmlNode element)
        {
            switch (element.Name.ToLowerInvariant())
            {
                case "h1":
                    return new Paragraph(ProcessInlineNodes(element.ChildNodes))
                    {
                        FontSize = 24,
                        FontWeight = FontWeights.Bold,
                        Margin = new Thickness(0, 10, 0, 10)
                    };

                case "h2":
                    return new Paragraph(ProcessInlineNodes(element.ChildNodes))
                    {
                        FontSize = 20,
                        FontWeight = FontWeights.Bold,
                        Margin = new Thickness(0, 8, 0, 8)
                    };

                case "h3":
                    return new Paragraph(ProcessInlineNodes(element.ChildNodes))
                    {
                        FontSize = 16,
                        FontWeight = FontWeights.Bold,
                        Margin = new Thickness(0, 6, 0, 6)
                    };

                case "p":
                    return new Paragraph(ProcessInlineNodes(element.ChildNodes));

                case "ul":
                    return ProcessList(element, false);

                case "ol":
                    return ProcessList(element, true);

                case "div":
                case "section":
                case "article":
                    // Para elementos de bloque genéricos, procesamos sus hijos
                    return ProcessBlockContainer(element);

                default:
                    // Para elementos no reconocidos que pueden contener texto
                    var inlineContent = ProcessInlineNodes(element.ChildNodes);
                    if (inlineContent != null && HasContent(inlineContent))
                    {
                        return new Paragraph(inlineContent);
                    }
                    break;
            }

            return null;
        }

        private Block ProcessBlockContainer(HtmlNode container)
        {
            var section = new Section();

            foreach (var child in container.ChildNodes)
            {
                var block = ProcessNode(child);
                if (block != null)
                {
                    section.Blocks.Add(block);
                }
            }

            return section.Blocks.Count > 0 ? section : null;
        }

        private List ProcessList(HtmlNode listElement, bool isOrdered)
        {
            var list = new List();

            foreach (var child in listElement.ChildNodes)
            {
                if (child.NodeType == HtmlNodeType.Element && child.Name.ToLowerInvariant() == "li")
                {
                    var listItem = new ListItem();

                    // Procesamos el contenido del <li>, que puede incluir párrafos, títulos, etc.
                    ProcessListItemContent(child, listItem.Blocks);

                    if (listItem.Blocks.Count > 0)
                    {
                        list.ListItems.Add(listItem);
                    }
                }
            }

            list.MarkerStyle = isOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc;

            return list;
        }

        private void ProcessListItemContent(HtmlNode liNode, BlockCollection blocks)
        {
            // Primero recolectamos todos los elementos de bloque (p, h1, h2, etc.)
            var blockElements = new List<HtmlNode>();
            var inlineContent = new List<HtmlNode>();

            foreach (var child in liNode.ChildNodes)
            {
                if (child.NodeType == HtmlNodeType.Element)
                {
                    switch (child.Name.ToLowerInvariant())
                    {
                        case "p":
                        case "h1":
                        case "h2":
                        case "h3":
                        case "h4":
                        case "h5":
                        case "h6":
                        case "ul":
                        case "ol":
                        case "div":
                            blockElements.Add(child);
                            break;
                        default:
                            inlineContent.Add(child);
                            break;
                    }
                }
                else if (child.NodeType == HtmlNodeType.Text && !string.IsNullOrWhiteSpace(child.InnerText))
                {
                    inlineContent.Add(child);
                }
            }

            // Si hay contenido inline al principio, lo añadimos como párrafo
            if (inlineContent.Count > 0)
            {
                var paragraph = new Paragraph();
                foreach (var node in inlineContent)
                {
                    var inline = ProcessInlineNode(node);
                    if (inline != null)
                    {
                        paragraph.Inlines.Add(inline);
                    }
                }
                if (paragraph.Inlines.Count > 0)
                {
                    blocks.Add(paragraph);
                }
            }

            // Procesamos los elementos de bloque
            foreach (var blockElement in blockElements)
            {
                var block = ProcessElement(blockElement);
                if (block != null)
                {
                    blocks.Add(block);
                }
            }

            // Si no hay contenido, añadimos un párrafo vacío
            if (blocks.Count == 0)
            {
                blocks.Add(new Paragraph());
            }
        }

        private Inline ProcessInlineNodes(HtmlNodeCollection nodes)
        {
            var span = new Span();
            bool hasContent = false;

            foreach (var node in nodes)
            {
                var inline = ProcessInlineNode(node);
                if (inline != null)
                {
                    span.Inlines.Add(inline);
                    hasContent = true;
                }
            }

            return hasContent ? span : null;
        }

        private Inline ProcessInlineNode(HtmlNode node)
        {
            if (node == null)
            {
                return null;
            }

            switch (node.NodeType)
            {
                case HtmlNodeType.Text:
                    var text = HtmlEntity.DeEntitize(node.InnerText);
                    if (!string.IsNullOrEmpty(text))
                    {
                        return new Run(text);
                    }
                    break;

                case HtmlNodeType.Element:
                    return ProcessInlineElement(node);
            }

            return null;
        }

        private Inline ProcessInlineElement(HtmlNode element)
        {
            switch (element.Name.ToLowerInvariant())
            {
                case "a":
                    var href = element.GetAttributeValue("href", "");
                    if (!string.IsNullOrEmpty(href))
                    {
                        var link = new Hyperlink(ProcessInlineNodes(element.ChildNodes) ?? new Run(element.InnerText));
                        if (Uri.TryCreate(href, UriKind.RelativeOrAbsolute, out Uri uri))
                        {
                            link.NavigateUri = uri;
                            // Siempre asignamos el manejador (ya sea el personalizado o el por defecto)
                            link.RequestNavigate += _hyperlinkHandler;
                        }
                        return link;
                    }
                    break;

                case "strong":
                case "b":
                    var strongContent = ProcessInlineNodes(element.ChildNodes);
                    if (strongContent != null)
                    {
                        var boldSpan = new Span(strongContent) { FontWeight = FontWeights.Bold };
                        return boldSpan;
                    }
                    break;

                case "em":
                case "i":
                    var emContent = ProcessInlineNodes(element.ChildNodes);
                    if (emContent != null)
                    {
                        var italicSpan = new Span(emContent) { FontStyle = FontStyles.Italic };
                        return italicSpan;
                    }
                    break;

                case "br":
                    return new LineBreak();

                case "span":
                    return ProcessInlineNodes(element.ChildNodes);

                default:
                    // Para elementos inline no reconocidos, procesamos su contenido
                    return ProcessInlineNodes(element.ChildNodes);
            }

            return null;
        }

        private bool HasContent(Inline inline)
        {
            if (inline is Run run)
            {
                return !string.IsNullOrWhiteSpace(run.Text);
            }

            if (inline is Span span)
            {
                foreach (var child in span.Inlines)
                {
                    if (HasContent(child))
                    {
                        return true;
                    }
                }
            }

            return inline is LineBreak or Hyperlink;
        }
    }
}