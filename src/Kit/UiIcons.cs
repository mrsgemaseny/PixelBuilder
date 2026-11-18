using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace PixelBuilder.Kit
{
    public enum Glyph
    {
        None, Pencil, Brush, Line, Rect, Fill, Pipette, Eraser, Select, Route, Undo, Redo, Save, Open, More, Grid, Target,
        Eye, EyeOff, Lock, Unlock, Check, Warning, Info, Error, Plus, Minus, Trash, Rotate, Roof, Image, Copy, Paste, Cut,
        Close, ChevronRight, ChevronDown, Map, Layers, People, Matrix, Clock, Import, Export, Sun, Moon, Shield, Hand
    }

    // Векторные иконки в стиле контурных системных значков: рисуются кодом, без картинок и эмодзи,
    // чётко на любом масштабе. Сетка 24 x 24, линия 1.8, скруглённые концы.
    public static class UiIcons
    {
        public static void Draw(Graphics g, Glyph glyph, RectangleF box, Color color)
        {
            if (glyph == Glyph.None) return;
            GraphicsState state = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float k = Math.Min(box.Width, box.Height) / 24f;
            g.TranslateTransform(box.X + (box.Width - 24 * k) / 2f, box.Y + (box.Height - 24 * k) / 2f);
            g.ScaleTransform(k, k);
            using (var pen = new Pen(color, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            using (var fill = new SolidBrush(color))
                Paint(g, glyph, pen, fill);
            g.Restore(state);
        }

        private static void Paint(Graphics g, Glyph glyph, Pen p, Brush f)
        {
            switch (glyph)
            {
                case Glyph.Pencil:
                    g.DrawPolygon(p, new[] { P(5, 19), P(6, 15), P(16, 5), P(19, 8), P(9, 18) });
                    g.DrawLine(p, 14, 7, 17, 10);
                    break;
                case Glyph.Brush:
                    g.DrawLine(p, 19, 4, 11, 13);
                    using (var path = new GraphicsPath())
                    {
                        path.AddBezier(11, 13, 7, 12, 5, 15, 5, 19);
                        path.AddBezier(5, 19, 9, 19, 12, 17, 11, 13);
                        g.DrawPath(p, path);
                    }
                    break;
                case Glyph.Line: g.DrawLine(p, 5, 19, 19, 5); g.FillEllipse(f, 3.5f, 17.5f, 3, 3); g.FillEllipse(f, 17.5f, 3.5f, 3, 3); break;
                case Glyph.Rect: Round(g, p, 5, 6, 14, 12, 2); break;
                case Glyph.Fill:
                    g.DrawPolygon(p, new[] { P(5, 11), P(11, 5), P(18, 12), P(12, 18) });
                    g.DrawLine(p, 5, 11, 18, 12);
                    using (var drop = new GraphicsPath())
                    {
                        drop.AddBezier(19.5f, 14, 21, 16.5f, 21.5f, 18, 19.5f, 19);
                        drop.AddBezier(19.5f, 19, 17.5f, 18, 18, 16.5f, 19.5f, 14);
                        g.FillPath(f, drop);
                    }
                    break;
                case Glyph.Pipette:
                    g.DrawLine(p, 5, 19, 13, 11);
                    g.DrawLine(p, 5, 19, 6, 16);
                    Round(g, p, 13.5f, 4.5f, 6, 6, 3);
                    g.DrawLine(p, 11, 9, 15, 13);
                    break;
                case Glyph.Eraser:
                    g.DrawPolygon(p, new[] { P(4, 15), P(13, 6), P(19, 12), P(12, 19), P(8, 19) });
                    g.DrawLine(p, 8.5f, 10.5f, 14.5f, 16.5f);
                    g.DrawLine(p, 12, 19, 20, 19);
                    break;
                case Glyph.Select:
                    using (var dash = (Pen)p.Clone()) { dash.DashPattern = new[] { 1.6f, 1.6f }; Round(g, dash, 4, 5, 16, 14, 1.5f); }
                    break;
                case Glyph.Route:
                    g.DrawEllipse(p, 4, 15, 4, 4);
                    g.DrawEllipse(p, 16, 5, 4, 4);
                    using (var path = new GraphicsPath()) { path.AddBezier(8, 17, 14, 17, 10, 7, 16, 7); g.DrawPath(p, path); }
                    break;
                case Glyph.Undo:
                    using (var path = new GraphicsPath()) { path.AddBezier(6, 10, 9, 6, 19, 6, 19, 13); path.AddBezier(19, 13, 19, 17, 15, 19, 12, 19); g.DrawPath(p, path); }
                    g.DrawLines(p, new[] { P(5, 5), P(6, 10), P(11, 9.5f) });
                    break;
                case Glyph.Redo:
                    using (var path = new GraphicsPath()) { path.AddBezier(18, 10, 15, 6, 5, 6, 5, 13); path.AddBezier(5, 13, 5, 17, 9, 19, 12, 19); g.DrawPath(p, path); }
                    g.DrawLines(p, new[] { P(19, 5), P(18, 10), P(13, 9.5f) });
                    break;
                case Glyph.Save:
                    Round(g, p, 5, 5, 14, 14, 2);
                    g.DrawLine(p, 9, 5, 9, 9); g.DrawLine(p, 9, 9, 15, 9); g.DrawLine(p, 15, 9, 15, 5);
                    Round(g, p, 8.5f, 13, 7, 6, 1);
                    break;
                case Glyph.Open:
                    g.DrawLines(p, new[] { P(4, 18), P(4, 7), P(9, 7), P(11, 9), P(19, 9), P(19, 11) });
                    g.DrawLines(p, new[] { P(4, 18), P(7, 12), P(21, 12), P(18, 18), P(4, 18) });
                    break;
                case Glyph.More: g.FillEllipse(f, 4.5f, 10.5f, 3, 3); g.FillEllipse(f, 10.5f, 10.5f, 3, 3); g.FillEllipse(f, 16.5f, 10.5f, 3, 3); break;
                case Glyph.Grid:
                    Round(g, p, 4, 4, 16, 16, 2);
                    g.DrawLine(p, 9.3f, 4, 9.3f, 20); g.DrawLine(p, 14.7f, 4, 14.7f, 20);
                    g.DrawLine(p, 4, 9.3f, 20, 9.3f); g.DrawLine(p, 4, 14.7f, 20, 14.7f);
                    break;
                case Glyph.Target:
                    g.DrawEllipse(p, 6, 6, 12, 12);
                    g.DrawLine(p, 12, 3, 12, 7); g.DrawLine(p, 12, 17, 12, 21); g.DrawLine(p, 3, 12, 7, 12); g.DrawLine(p, 17, 12, 21, 12);
                    g.FillEllipse(f, 10.5f, 10.5f, 3, 3);
                    break;
                case Glyph.Eye:
                case Glyph.EyeOff:
                    using (var path = new GraphicsPath()) { path.AddBezier(3, 12, 7, 5, 17, 5, 21, 12); path.AddBezier(21, 12, 17, 19, 7, 19, 3, 12); g.DrawPath(p, path); }
                    g.DrawEllipse(p, 9.5f, 9.5f, 5, 5);
                    if (glyph == Glyph.EyeOff) g.DrawLine(p, 4, 4, 20, 20);
                    break;
                case Glyph.Lock:
                case Glyph.Unlock:
                    Round(g, p, 6, 11, 12, 9, 2);
                    if (glyph == Glyph.Lock) g.DrawArc(p, 8, 4, 8, 10, 180, 180);
                    else g.DrawArc(p, 8, 4, 8, 10, 180, 140);
                    if (glyph == Glyph.Lock) { g.DrawLine(p, 8, 9, 8, 11); g.DrawLine(p, 16, 9, 16, 11); }
                    else g.DrawLine(p, 8, 9, 8, 11);
                    break;
                case Glyph.Check: g.DrawLines(p, new[] { P(5, 12.5f), P(10, 17), P(19, 7) }); break;
                case Glyph.Warning:
                    g.DrawPolygon(p, new[] { P(12, 4), P(21, 19.5f), P(3, 19.5f) });
                    g.DrawLine(p, 12, 10, 12, 14); g.FillEllipse(f, 11, 16, 2, 2);
                    break;
                case Glyph.Info:
                    g.DrawEllipse(p, 3.5f, 3.5f, 17, 17);
                    g.DrawLine(p, 12, 11, 12, 16.5f); g.FillEllipse(f, 11, 7, 2, 2);
                    break;
                case Glyph.Error:
                    g.DrawEllipse(p, 3.5f, 3.5f, 17, 17);
                    g.DrawLine(p, 9, 9, 15, 15); g.DrawLine(p, 15, 9, 9, 15);
                    break;
                case Glyph.Plus: g.DrawLine(p, 12, 5, 12, 19); g.DrawLine(p, 5, 12, 19, 12); break;
                case Glyph.Minus: g.DrawLine(p, 5, 12, 19, 12); break;
                case Glyph.Trash:
                    g.DrawLine(p, 4, 7, 20, 7);
                    g.DrawLines(p, new[] { P(6, 7), P(7, 20), P(17, 20), P(18, 7) });
                    g.DrawLines(p, new[] { P(9, 7), P(9.5f, 4), P(14.5f, 4), P(15, 7) });
                    g.DrawLine(p, 10, 11, 10, 16); g.DrawLine(p, 14, 11, 14, 16);
                    break;
                case Glyph.Rotate:
                    g.DrawArc(p, 5, 5, 14, 14, -60, 300);
                    g.DrawLines(p, new[] { P(15, 3.5f), P(16, 7.5f), P(12, 8.5f) });
                    break;
                case Glyph.Roof:
                    g.DrawLines(p, new[] { P(3, 12), P(12, 4), P(21, 12) });
                    g.DrawLines(p, new[] { P(6, 10), P(6, 19), P(18, 19), P(18, 10) });
                    break;
                case Glyph.Image:
                    Round(g, p, 3.5f, 5, 17, 14, 2);
                    g.DrawLines(p, new[] { P(4, 17), P(9, 12), P(13, 16), P(15, 14), P(20, 18) });
                    g.DrawEllipse(p, 14, 8, 3, 3);
                    break;
                case Glyph.Copy: Round(g, p, 8, 8, 12, 12, 2); g.DrawLines(p, new[] { P(5, 15), P(4, 15), P(4, 5), P(5, 4), P(15, 4), P(15, 5) }); break;
                case Glyph.Paste:
                    Round(g, p, 5, 5, 14, 16, 2);
                    Round(g, p, 9, 3, 6, 4, 1);
                    g.DrawLine(p, 9, 12, 15, 12); g.DrawLine(p, 9, 16, 13, 16);
                    break;
                case Glyph.Cut:
                    g.DrawEllipse(p, 4, 15, 5, 5); g.DrawEllipse(p, 15, 15, 5, 5);
                    g.DrawLine(p, 8.5f, 15.5f, 17, 4); g.DrawLine(p, 15.5f, 15.5f, 7, 4);
                    break;
                case Glyph.Close: g.DrawLine(p, 6, 6, 18, 18); g.DrawLine(p, 18, 6, 6, 18); break;
                case Glyph.ChevronRight: g.DrawLines(p, new[] { P(9, 5), P(16, 12), P(9, 19) }); break;
                case Glyph.ChevronDown: g.DrawLines(p, new[] { P(5, 9), P(12, 16), P(19, 9) }); break;
                case Glyph.Map:
                    g.DrawPolygon(p, new[] { P(3, 6), P(9, 4), P(15, 6), P(21, 4), P(21, 18), P(15, 20), P(9, 18), P(3, 20) });
                    g.DrawLine(p, 9, 4, 9, 18); g.DrawLine(p, 15, 6, 15, 20);
                    break;
                case Glyph.Layers:
                    g.DrawPolygon(p, new[] { P(12, 4), P(21, 9), P(12, 14), P(3, 9) });
                    g.DrawLines(p, new[] { P(3, 13), P(12, 18), P(21, 13) });
                    break;
                case Glyph.People:
                    g.DrawEllipse(p, 6, 4, 6, 6); g.DrawArc(p, 3, 12, 12, 10, 180, 180);
                    g.DrawEllipse(p, 14, 6, 5, 5); g.DrawArc(p, 13, 13, 9, 8, 200, 140);
                    break;
                case Glyph.Matrix:
                    for (int y = 0; y < 3; y++)
                        for (int x = 0; x < 3; x++) g.FillEllipse(f, 4.5f + x * 6, 4.5f + y * 6, 3.4f, 3.4f);
                    break;
                case Glyph.Clock: g.DrawEllipse(p, 3.5f, 3.5f, 17, 17); g.DrawLines(p, new[] { P(12, 7), P(12, 12), P(15.5f, 14) }); break;
                case Glyph.Import:
                    g.DrawLine(p, 12, 3, 12, 14); g.DrawLines(p, new[] { P(7.5f, 9.5f), P(12, 14), P(16.5f, 9.5f) });
                    g.DrawLines(p, new[] { P(4, 15), P(4, 20), P(20, 20), P(20, 15) });
                    break;
                case Glyph.Export:
                    g.DrawLine(p, 12, 14, 12, 3); g.DrawLines(p, new[] { P(7.5f, 7.5f), P(12, 3), P(16.5f, 7.5f) });
                    g.DrawLines(p, new[] { P(4, 15), P(4, 20), P(20, 20), P(20, 15) });
                    break;
                case Glyph.Sun:
                    g.DrawEllipse(p, 8, 8, 8, 8);
                    for (int i = 0; i < 8; i++)
                    {
                        double a = i * Math.PI / 4;
                        g.DrawLine(p, 12 + (float)Math.Cos(a) * 6.5f, 12 + (float)Math.Sin(a) * 6.5f, 12 + (float)Math.Cos(a) * 9, 12 + (float)Math.Sin(a) * 9);
                    }
                    break;
                case Glyph.Moon:
                    using (var path = new GraphicsPath()) { path.AddBezier(15, 4, 7, 4, 4, 15, 12, 19); path.AddBezier(12, 19, 16, 21, 20, 18, 20, 15); path.AddBezier(20, 15, 13, 17, 10, 9, 15, 4); g.DrawPath(p, path); }
                    break;
                case Glyph.Shield:
                    g.DrawPolygon(p, new[] { P(12, 3), P(19, 6), P(19, 12), P(12, 21), P(5, 12), P(5, 6) });
                    g.DrawLines(p, new[] { P(8.5f, 12), P(11, 14.5f), P(15.5f, 9.5f) });
                    break;
                case Glyph.Hand:
                    Round(g, p, 6, 9, 12, 11, 4);
                    g.DrawLine(p, 9, 9, 9, 5); g.DrawLine(p, 12, 9, 12, 4); g.DrawLine(p, 15, 9, 15, 5);
                    break;
            }
        }

        private static PointF P(float x, float y) { return new PointF(x, y); }

        private static void Round(Graphics g, Pen p, float x, float y, float w, float h, float r)
        {
            using (GraphicsPath path = Ui.Round(new RectangleF(x, y, w, h), r)) g.DrawPath(p, path);
        }
    }
}
