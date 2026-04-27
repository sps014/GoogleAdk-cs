using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Spectre.Console;
using Spectre.Console.Rendering;
using System.Text;

namespace GoogleAdk.Core.Runner;

/// <summary>
/// Renders Markdown text into Spectre.Console IRenderable objects.
/// </summary>
public static class MarkdownConsoleRenderer
{
    /// <summary>
    /// Renders the given markdown string into a Spectre.Console IRenderable.
    /// </summary>
    public static IRenderable Render(string markdown)
    {
        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        var document = Markdown.Parse(markdown, pipeline);
        
        var renderables = new List<IRenderable>();
        foreach (var block in document)
        {
            var renderable = RenderBlock(block);
            if (renderable != null)
            {
                renderables.Add(renderable);
                renderables.Add(new Text("")); // Add empty line between blocks
            }
        }
        
        return new Rows(renderables);
    }

    private static IRenderable? RenderBlock(Block block)
    {
        switch (block)
        {
            case ParagraphBlock p:
                return new Markup(RenderInlines(p.Inline));
            
            case HeadingBlock h:
                var text = RenderInlines(h.Inline);
                var color = h.Level switch
                {
                    1 => "blue",
                    2 => "green",
                    3 => "yellow",
                    4 => "magenta",
                    5 => "cyan",
                    _ => "white"
                };
                return new Markup($"[{color} bold]{Markup.Escape(new string('#', h.Level))} {text}[/]");

            case Markdig.Extensions.Mathematics.MathBlock math:
                return new Panel(Markup.Escape(math.Lines.ToString().TrimEnd()))
                    .BorderColor(Color.Grey)
                    .Header("[grey]math[/]")
                    .Expand();

            case FencedCodeBlock c:
                var code = c.Lines.ToString().TrimEnd();
                var panel = new Panel(Markup.Escape(code))
                    .BorderColor(Color.Grey)
                    .Header(string.IsNullOrEmpty(c.Info) ? "" : $"[grey]{Markup.Escape(c.Info)}[/]")
                    .Expand();
                return panel;

            case CodeBlock c:
                return new Panel(Markup.Escape(c.Lines.ToString().TrimEnd()))
                    .BorderColor(Color.Grey)
                    .Expand();

            case ListBlock l:
                return RenderList(l, 0);

            case Markdig.Extensions.Alerts.AlertBlock a:
                var alertRenderables = new List<IRenderable>();
                foreach (var child in a)
                {
                    var r = RenderBlock(child);
                    if (r != null) alertRenderables.Add(r);
                }
                
                var alertKind = a.Kind.ToString().ToLowerInvariant();
                var alertColor = alertKind switch
                {
                    "note" => Color.Blue,
                    "tip" => Color.Green,
                    "important" => Color.Purple,
                    "warning" => Color.Yellow,
                    "caution" => Color.Red,
                    _ => Color.Grey
                };
                
                return new Panel(new Rows(alertRenderables))
                    .Header($"[{alertColor.ToMarkup()}]{a.Kind.ToString().ToUpper()}[/]")
                    .BorderColor(alertColor)
                    .RoundedBorder()
                    .Expand();

            case QuoteBlock q:
                var quoteRenderables = new List<IRenderable>();
                foreach (var child in q)
                {
                    var r = RenderBlock(child);
                    if (r != null) quoteRenderables.Add(r);
                }
                return new Panel(new Rows(quoteRenderables))
                    .BorderColor(Color.Yellow)
                    .NoSafeBorder()
                    .PadLeft(1);

            case Markdig.Extensions.Tables.Table t:
                return RenderTable(t);

            case Markdig.Extensions.CustomContainers.CustomContainer customContainer:
                var containerRenderables = new List<IRenderable>();
                foreach (var child in customContainer)
                {
                    var r = RenderBlock(child);
                    if (r != null) containerRenderables.Add(r);
                }
                return new Panel(new Rows(containerRenderables))
                    .BorderColor(Color.Grey)
                    .Header(string.IsNullOrEmpty(customContainer.Info) ? "" : $"[grey]{Markup.Escape(customContainer.Info)}[/]")
                    .Expand();

            case Markdig.Extensions.Footnotes.FootnoteGroup footnoteGroup:
                var footnoteRenderables = new List<IRenderable>();
                footnoteRenderables.Add(new Rule("[grey]Footnotes[/]").RuleStyle("grey"));
                foreach (var child in footnoteGroup)
                {
                    var r = RenderBlock(child);
                    if (r != null) footnoteRenderables.Add(r);
                }
                return new Rows(footnoteRenderables);

            case Markdig.Extensions.Footnotes.Footnote footnote:
                var fnRenderables = new List<IRenderable>();
                foreach (var child in footnote)
                {
                    var r = RenderBlock(child);
                    if (r != null) fnRenderables.Add(r);
                }
                var label = $"[blue]{Markup.Escape("[" + (footnote.Label ?? "") + "]")}[/]";
                var grid = new Grid().AddColumn(new GridColumn().NoWrap()).AddColumn(new GridColumn());
                grid.AddRow(new Markup(label), new Rows(fnRenderables));
                return grid;

            case Markdig.Extensions.DefinitionLists.DefinitionList definitionList:
                var defRenderables = new List<IRenderable>();
                foreach (var child in definitionList)
                {
                    if (child is Markdig.Extensions.DefinitionLists.DefinitionItem defItem)
                    {
                        foreach (var defChild in defItem)
                        {
                            if (defChild is Markdig.Extensions.DefinitionLists.DefinitionTerm term)
                            {
                                defRenderables.Add(new Markup($"[bold]{RenderInlines(term.Inline)}[/]"));
                            }
                            else if (defChild is Markdig.Extensions.DefinitionLists.DefinitionItem definition)
                            {
                                var innerDefs = new List<IRenderable>();
                                foreach (var inner in definition)
                                {
                                    var r = RenderBlock(inner);
                                    if (r != null) innerDefs.Add(r);
                                }
                                defRenderables.Add(new Padder(new Rows(innerDefs)).PadLeft(2));
                            }
                        }
                    }
                }
                return new Rows(defRenderables);

            case Markdig.Extensions.Figures.Figure figure:
                var figRenderables = new List<IRenderable>();
                foreach (var child in figure)
                {
                    if (child is Markdig.Extensions.Figures.FigureCaption caption)
                    {
                        var captionRenderables = new List<IRenderable>();
                        // FigureCaption inherits from LeafBlock, so it contains Inline
                        if (caption.Inline != null)
                        {
                            foreach (var capChild in caption.Inline)
                            {
                                captionRenderables.Add(new Markup(RenderInline(capChild)));
                            }
                        }
                        figRenderables.Add(new Markup($"[italic]{string.Join(" ", captionRenderables.Select(x => x.ToString() ?? ""))}[/]"));
                    }
                    else
                    {
                        var r = RenderBlock(child);
                        if (r != null) figRenderables.Add(r);
                    }
                }
                return new Rows(figRenderables);

            case Markdig.Extensions.Footers.FooterBlock footer:
                var footerRenderables = new List<IRenderable>();
                footerRenderables.Add(new Rule().RuleStyle("grey"));
                foreach (var child in footer)
                {
                    var r = RenderBlock(child);
                    if (r != null) footerRenderables.Add(r);
                }
                return new Rows(footerRenderables);

            case ThematicBreakBlock:
                return new Rule().RuleStyle("grey");

            case Markdig.Syntax.HtmlBlock htmlBlock:
                return new Markup(Markup.Escape(htmlBlock.Lines.ToString().TrimEnd()));

            default:
                if (block is LeafBlock leaf)
                {
                    return new Markup(RenderInlines(leaf.Inline));
                }
                return null;
        }
    }

    private static IRenderable RenderTable(Markdig.Extensions.Tables.Table table)
    {
        var spectreTable = new Table()
            .Border(TableBorder.Rounded)
            .Expand();

        bool isFirstRow = true;
        foreach (var block in table)
        {
            if (block is Markdig.Extensions.Tables.TableRow row)
            {
                var cells = new List<IRenderable>();
                foreach (var cellBlock in row)
                {
                    if (cellBlock is Markdig.Extensions.Tables.TableCell cell)
                    {
                        var cellRenderables = new List<IRenderable>();
                        foreach (var cellChild in cell)
                        {
                            var r = RenderBlock(cellChild);
                            if (r != null) cellRenderables.Add(r);
                        }
                        cells.Add(new Rows(cellRenderables));
                    }
                }

                if (isFirstRow)
                {
                    if (row.IsHeader)
                    {
                        foreach (var cell in cells)
                        {
                            spectreTable.AddColumn(new TableColumn(cell));
                        }
                    }
                    else
                    {
                        foreach (var cell in cells)
                        {
                            spectreTable.AddColumn(new TableColumn(""));
                        }
                        spectreTable.AddRow(cells.ToArray());
                    }
                    isFirstRow = false;
                }
                else
                {
                    while (spectreTable.Columns.Count < cells.Count)
                    {
                        spectreTable.AddColumn(new TableColumn(""));
                    }
                    spectreTable.AddRow(cells.ToArray());
                }
            }
        }

        return spectreTable;
    }

    private static IRenderable RenderList(ListBlock list, int depth)
    {
        var rows = new List<IRenderable>();
        int index = 1;
        foreach (var item in list)
        {
            if (item is ListItemBlock listItem)
            {
                var bullet = list.IsOrdered ? $"{index++}." : "•";
                var indent = new string(' ', depth * 2);
                
                var itemRenderables = new List<IRenderable>();
                foreach (var child in listItem)
                {
                    if (child is ListBlock nestedList)
                    {
                        itemRenderables.Add(RenderList(nestedList, depth + 1));
                    }
                    else if (child is ParagraphBlock p)
                    {
                        var taskListExt = p.Inline?.FirstOrDefault(i => i is Markdig.Extensions.TaskLists.TaskList) as Markdig.Extensions.TaskLists.TaskList;
                        var taskPrefix = "";
                        if (taskListExt != null)
                        {
                            taskPrefix = taskListExt.Checked ? "[green][[x]][/] " : "[grey][[ ]][/] ";
                        }
                        
                        itemRenderables.Add(new Markup($"{indent}[bold]{bullet}[/] {taskPrefix}{RenderInlines(p.Inline)}"));
                    }
                    else
                    {
                        var r = RenderBlock(child);
                        if (r != null) itemRenderables.Add(r);
                    }
                }
                rows.Add(new Rows(itemRenderables));
            }
        }
        return new Rows(rows);
    }

    private static string RenderInlines(ContainerInline? inlines)
    {
        if (inlines == null) return string.Empty;

        var sb = new StringBuilder();
        foreach (var inline in inlines)
        {
            sb.Append(RenderInline(inline));
        }
        return sb.ToString();
    }

    private static string RenderInline(Inline inline)
    {
        switch (inline)
        {
            case Markdig.Extensions.TaskLists.TaskList taskList:
                // We handle the visual rendering of the checkbox in the ListBlock rendering,
                // so we just return empty string here to avoid duplicating it.
                return string.Empty;

            case Markdig.Extensions.Emoji.EmojiInline emoji:
                return Emoji.Replace(emoji.Content.ToString());

            case Markdig.Extensions.Footnotes.FootnoteLink footnoteLink:
                return $"[blue]{Markup.Escape("[" + (footnoteLink.Footnote?.Label ?? "") + "]")}[/]";

            case LiteralInline literal:
                return Markup.Escape(literal.Content.ToString());

            case CodeInline code:
                return $"[grey]{Markup.Escape(code.Content)}[/]";

            case Markdig.Extensions.Mathematics.MathInline mathInline:
                return $"[grey]{Markup.Escape(mathInline.Content.ToString())}[/]";

            case EmphasisInline emphasis:
                var inner = RenderInlines(emphasis);
                if (emphasis.DelimiterChar == '*')
                {
                    return emphasis.DelimiterCount >= 2 ? $"[bold]{inner}[/]" : $"[italic]{inner}[/]";
                }
                else if (emphasis.DelimiterChar == '_')
                {
                    return emphasis.DelimiterCount >= 2 ? $"[bold]{inner}[/]" : $"[italic]{inner}[/]";
                }
                else if (emphasis.DelimiterChar == '~')
                {
                    return emphasis.DelimiterCount >= 2 ? $"[strikethrough]{inner}[/]" : inner; // ~subscript~ is unsupported
                }
                else if (emphasis.DelimiterChar == '^')
                {
                    return inner; // ^superscript^ is unsupported
                }
                else if (emphasis.DelimiterChar == '+')
                {
                    return $"[underline]{inner}[/]"; // ++inserted++
                }
                else if (emphasis.DelimiterChar == '=')
                {
                    return $"[black on yellow]{inner}[/]"; // ==marked==
                }
                return inner;

            case LinkInline link:
                var text = RenderInlines(link);
                if (link.IsImage)
                {
                    return $"[blue]![{text}]({Markup.Escape(link.Url ?? "")})[/]";
                }
                else
                {
                    return $"[link={Markup.Escape(link.Url ?? "")}]{text}[/]";
                }

            case AutolinkInline autolink:
                return $"[link={Markup.Escape(autolink.Url ?? "")}]{Markup.Escape(autolink.Url ?? "")}[/]";

            case LineBreakInline:
                return "\n";

            case Markdig.Syntax.Inlines.HtmlInline html:
                return Markup.Escape(html.Tag);

            case ContainerInline container:
                return RenderInlines(container);

            default:
                return string.Empty;
        }
    }
}
