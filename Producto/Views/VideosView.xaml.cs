using HtmlAgilityPack;
using Nesto.Modules.Producto.Models;
using Nesto.Modules.Producto.ViewModels;
using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;

namespace Nesto.Modules.Producto.Views
{
    public partial class VideosView : UserControl
    {
        private VideosViewModel _viewModel;

        public VideosView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            _viewModel = DataContext as VideosViewModel;
            if (_viewModel != null)
            {
                _viewModel.VideoCompletoSeleccionadoCambiado += OnVideoCompletoSeleccionadoCambiado;
            }
        }

        private void OnVideoCompletoSeleccionadoCambiado(VideoModel video)
        {
            if (video == null)
            {
                DescripcionRichTextBox?.Document.Blocks.Clear();
                ProtocoloRichTextBox?.Document.Blocks.Clear();
                return;
            }

            // Actualizar descripcion
            if (DescripcionRichTextBox != null)
            {
                DescripcionRichTextBox.Document.Blocks.Clear();
                if (!string.IsNullOrEmpty(video.Descripcion))
                {
                    var descripcionDocument = HtmlToFlowDocument(video.Descripcion);
                    foreach (var block in descripcionDocument.Blocks)
                    {
                        DescripcionRichTextBox.Document.Blocks.Add(CloneBlock(block));
                    }
                }
            }

            // Actualizar protocolo
            if (ProtocoloRichTextBox != null)
            {
                ProtocoloRichTextBox.Document.Blocks.Clear();
                if (!string.IsNullOrEmpty(video.Protocolo))
                {
                    var protocoloDocument = HtmlToFlowDocument(video.Protocolo);
                    foreach (var block in protocoloDocument.Blocks)
                    {
                        ProtocoloRichTextBox.Document.Blocks.Add(CloneBlock(block));
                    }
                }
            }
        }

        private static Block CloneBlock(Block block)
        {
            if (block is Paragraph paragraph)
            {
                var newParagraph = new Paragraph();
                foreach (var inline in paragraph.Inlines)
                {
                    newParagraph.Inlines.Add(CloneInline(inline));
                }
                return newParagraph;
            }
            return new Paragraph(new Run(block.ToString()));
        }

        private static Inline CloneInline(Inline inline)
        {
            if (inline is Run run)
            {
                return new Run(run.Text)
                {
                    FontWeight = run.FontWeight,
                    FontStyle = run.FontStyle,
                    TextDecorations = run.TextDecorations,
                    Foreground = run.Foreground
                };
            }
            else if (inline is Hyperlink hyperlink)
            {
                var newHyperlink = new Hyperlink { NavigateUri = hyperlink.NavigateUri };
                foreach (var hInline in hyperlink.Inlines)
                {
                    newHyperlink.Inlines.Add(CloneInline(hInline));
                }
                newHyperlink.RequestNavigate += Hyperlink_RequestNavigate;
                return newHyperlink;
            }
            else if (inline is Bold bold)
            {
                var newBold = new Bold();
                foreach (var bInline in bold.Inlines)
                {
                    newBold.Inlines.Add(CloneInline(bInline));
                }
                return newBold;
            }
            else if (inline is Italic italic)
            {
                var newItalic = new Italic();
                foreach (var iInline in italic.Inlines)
                {
                    newItalic.Inlines.Add(CloneInline(iInline));
                }
                return newItalic;
            }
            else if (inline is Underline underline)
            {
                var newUnderline = new Underline();
                foreach (var uInline in underline.Inlines)
                {
                    newUnderline.Inlines.Add(CloneInline(uInline));
                }
                return newUnderline;
            }
            else if (inline is LineBreak)
            {
                return new LineBreak();
            }
            return new Run(inline.ToString());
        }

        private static void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        private FlowDocument HtmlToFlowDocument(string html)
        {
            var document = new FlowDocument();

            if (string.IsNullOrEmpty(html))
            {
                return document;
            }

            try
            {
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);

                var paragraph = new Paragraph();
                ProcessHtmlNodes(htmlDoc.DocumentNode, paragraph.Inlines);
                document.Blocks.Add(paragraph);
            }
            catch
            {
                // Si falla el parsing HTML, mostrar como texto plano
                document.Blocks.Add(new Paragraph(new Run(html)));
            }

            return document;
        }

        private void ProcessHtmlNodes(HtmlNode node, InlineCollection inlines)
        {
            foreach (var child in node.ChildNodes)
            {
                switch (child.NodeType)
                {
                    case HtmlNodeType.Text:
                        var text = System.Net.WebUtility.HtmlDecode(child.InnerText);
                        if (!string.IsNullOrEmpty(text))
                        {
                            inlines.Add(new Run(text));
                        }
                        break;

                    case HtmlNodeType.Element:
                        switch (child.Name.ToLower())
                        {
                            case "br":
                                inlines.Add(new LineBreak());
                                break;

                            case "p":
                                ProcessHtmlNodes(child, inlines);
                                inlines.Add(new LineBreak());
                                inlines.Add(new LineBreak());
                                break;

                            case "b":
                            case "strong":
                                var bold = new Bold();
                                ProcessHtmlNodes(child, bold.Inlines);
                                inlines.Add(bold);
                                break;

                            case "i":
                            case "em":
                                var italic = new Italic();
                                ProcessHtmlNodes(child, italic.Inlines);
                                inlines.Add(italic);
                                break;

                            case "u":
                                var underline = new Underline();
                                ProcessHtmlNodes(child, underline.Inlines);
                                inlines.Add(underline);
                                break;

                            case "a":
                                var href = child.GetAttributeValue("href", "");
                                if (!string.IsNullOrEmpty(href))
                                {
                                    try
                                    {
                                        var hyperlink = new Hyperlink { NavigateUri = new Uri(href) };
                                        hyperlink.RequestNavigate += Hyperlink_RequestNavigate;
                                        ProcessHtmlNodes(child, hyperlink.Inlines);
                                        inlines.Add(hyperlink);
                                    }
                                    catch
                                    {
                                        ProcessHtmlNodes(child, inlines);
                                    }
                                }
                                else
                                {
                                    ProcessHtmlNodes(child, inlines);
                                }
                                break;

                            default:
                                ProcessHtmlNodes(child, inlines);
                                break;
                        }
                        break;
                }
            }
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            var video = _viewModel?.VideoCompletoSeleccionado;
            if (video != null && !string.IsNullOrEmpty(video.UrlVideo))
            {
                Process.Start(new ProcessStartInfo(video.UrlVideo) { UseShellExecute = true });
            }
            else if (_viewModel?.VideoSeleccionado != null && !string.IsNullOrEmpty(_viewModel.VideoSeleccionado.UrlVideo))
            {
                Process.Start(new ProcessStartInfo(_viewModel.VideoSeleccionado.UrlVideo) { UseShellExecute = true });
            }
        }

        private void RichTextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is RichTextBox richTextBox)
            {
                var position = e.GetPosition(richTextBox);
                var textPointer = richTextBox.GetPositionFromPoint(position, true);

                if (textPointer != null)
                {
                    var parent = textPointer.Parent;
                    while (parent != null && !(parent is Hyperlink))
                    {
                        parent = (parent as FrameworkContentElement)?.Parent;
                    }

                    if (parent is Hyperlink hyperlink && hyperlink.NavigateUri != null)
                    {
                        Process.Start(new ProcessStartInfo(hyperlink.NavigateUri.AbsoluteUri) { UseShellExecute = true });
                        e.Handled = true;
                    }
                }
            }
        }

        private void RichTextBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (sender is RichTextBox richTextBox)
            {
                var position = e.GetPosition(richTextBox);
                var textPointer = richTextBox.GetPositionFromPoint(position, true);

                if (textPointer != null)
                {
                    var parent = textPointer.Parent;
                    while (parent != null && !(parent is Hyperlink))
                    {
                        parent = (parent as FrameworkContentElement)?.Parent;
                    }

                    richTextBox.Cursor = parent is Hyperlink ? Cursors.Hand : Cursors.IBeam;
                }
            }
        }

        private void RichTextBox_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is RichTextBox richTextBox)
            {
                richTextBox.Cursor = Cursors.IBeam;
            }
        }
    }
}
