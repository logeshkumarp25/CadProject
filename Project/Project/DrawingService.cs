using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using ShapePath = System.Windows.Shapes.Path;

namespace Project
{
    public class DrawingService
    {
        private readonly Canvas canvas;
        private ShapePath? arcPath;
        private Point startPoint;

        public DrawingService(Canvas canvas) => this.canvas = canvas;

        public void DrawLine(Point p1, Point p2)
        {
            canvas.Children.Add(new Line
            {
                X1 = p1.X,
                Y1 = p1.Y,
                X2 = p2.X,
                Y2 = p2.Y,
                Stroke = Brushes.White,
                StrokeThickness = 2
            });
        }

        public void DrawCircle(Point center, double radius)
        {
            var circle = new Ellipse
            {
                Width = radius * 2,
                Height = radius * 2,
                Stroke = Brushes.Yellow,
                StrokeThickness = 2
            };
            Canvas.SetLeft(circle, center.X - radius);
            Canvas.SetTop(circle, center.Y - radius);
            canvas.Children.Add(circle);
        }

        public void StartArc(Point point)
        {
            startPoint = point;
            arcPath = new ShapePath { Stroke = Brushes.Red, StrokeThickness = 2 };
            var fig = new PathFigure { StartPoint = point };
            fig.Segments.Add(new ArcSegment { Point = point, Size = new Size(50, 50) });
            var geo = new PathGeometry();
            geo.Figures.Add(fig);
            arcPath.Data = geo;
            canvas.Children.Add(arcPath);
        }

        public void UpdateArc(Point point)
        {
            if (arcPath == null) return;
            double radius = Distance(startPoint, point);
            var geo = (PathGeometry)arcPath.Data;
            var fig = geo.Figures[0];
            var seg = (ArcSegment)fig.Segments[0];
            seg.Point = point;
            seg.Size = new Size(radius, radius);
            seg.SweepDirection = SweepDirection.Clockwise;
            seg.IsLargeArc = false;
        }

        public void FinishShape() => arcPath = null;

        public void MoveShape(Shape shape, Point oldPoint, Point newPoint)
        {
            double dx = newPoint.X - oldPoint.X;
            double dy = newPoint.Y - oldPoint.Y;

            if (shape is Line line)
            {
                line.X1 += dx; line.Y1 += dy;
                line.X2 += dx; line.Y2 += dy;
            }
            else if (shape is Ellipse ellipse)
            {
                Canvas.SetLeft(ellipse, Canvas.GetLeft(ellipse) + dx);
                Canvas.SetTop(ellipse, Canvas.GetTop(ellipse) + dy);
            }
            else if (shape is ShapePath path)
            {
                var transform = path.RenderTransform as TranslateTransform ?? new TranslateTransform();
                transform.X += dx;
                transform.Y += dy;
                path.RenderTransform = transform;
            }
        }
        public void DeleteShape(Shape shape) => canvas.Children.Remove(shape);
        public void DeleteAll() => canvas.Children.Clear();
        public Shape? SelectShape(Point clickPoint)
        {
            const double tolerance = 10.0;
            for (int i = canvas.Children.Count - 1; i >= 0; i--)
            {
                if (canvas.Children[i] is not Shape shape) continue;

                if (shape is Line line && IsPointNearLine(line, clickPoint, tolerance))
                    return line;

                if (shape is Ellipse circle && IsPointNearCircle(circle, clickPoint, tolerance))
                    return circle;

                if (shape is ShapePath path && IsPointNearArc(path, clickPoint, tolerance))
                    return path;
            }
            return null;
        }

        private bool IsPointNearLine(Line line, Point p, double tol)
        {
            Point a = new(line.X1, line.Y1), b = new(line.X2, line.Y2);
            double dx = b.X - a.X, dy = b.Y - a.Y;
            if (dx == 0 && dy == 0) return Distance(a, p) <= tol;
            double t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / (dx * dx + dy * dy);
            t = Math.Max(0, Math.Min(1, t));
            Point nearest = new(a.X + t * dx, a.Y + t * dy);
            return Distance(nearest, p) <= tol;
        }

        private bool IsPointNearCircle(Ellipse circle, Point p, double tol)
        {
            Point center = GetCircleCenter(circle);
            return Math.Abs(Distance(center, p) - circle.Width / 2) <= tol;
        }

        private bool IsPointNearArc(ShapePath arc, Point p, double tol)
        {
            if (arc.Data == null) return false;
            var pen = new Pen(Brushes.Transparent, arc.StrokeThickness + tol * 2);
            return arc.Data.StrokeContains(pen, p);
        }
        public void Trim(Shape cuttingShape, Shape trimShape, Point clickPoint)
        {
            if (cuttingShape is Line cLine && trimShape is Line tLine)
                TrimLineByLine(cLine, tLine, clickPoint);
            else if (cuttingShape is Line cutLine && trimShape is Ellipse trimCircle)
                TrimCircleByLine(cutLine, trimCircle, clickPoint);
            else if (cuttingShape is Ellipse cutCircle && trimShape is Line trimLine)
                TrimLineByCircle(cutCircle, trimLine, clickPoint);
            else if (cuttingShape is Line cutLine2 && trimShape is ShapePath trimArc)
                TrimArcByLine(cutLine2, trimArc, clickPoint);
            else if (cuttingShape is ShapePath cutArc && trimShape is Line trimLine2)
                TrimLineByArc(cutArc, trimLine2, clickPoint);
            else if (cuttingShape is ShapePath cutArc2 && trimShape is Ellipse trimCircle2)
                TrimCircleByArc(cutArc2, trimCircle2, clickPoint);
            else if (cuttingShape is Ellipse cutCircle2 && trimShape is ShapePath trimArc2)
                TrimArcByCircle(cutCircle2, trimArc2, clickPoint);
            else if (cuttingShape is Ellipse cutCircle3 && trimShape is Ellipse trimCircle3)
                TrimCircleByCircle(cutCircle3, trimCircle3, clickPoint);
        }

        private void TrimLineByLine(Line cutter, Line target, Point click)
        {
            if (!GetLineLineIntersection(cutter, target, out Point hit)) return;
            if (Distance(new(target.X1, target.Y1), click) < Distance(new(target.X2, target.Y2), click))
            { target.X1 = hit.X; target.Y1 = hit.Y; }
            else
            { target.X2 = hit.X; target.Y2 = hit.Y; }
        }

        private void TrimCircleByLine(Line cutter, Ellipse circle, Point click)
        {
            var pts = GetLineCircleIntersectionInfinite(cutter, circle);
            if (pts.Count < 2) return;

            Point center = GetCircleCenter(circle);
            double r = circle.Width / 2;

            var sorted = pts.OrderBy(p => Math.Atan2(p.Y - center.Y, p.X - center.X)).ToList();
            Point p0 = sorted[0], p1 = sorted[1];
            double a0 = Math.Atan2(p0.Y - center.Y, p0.X - center.X);
            double a1 = Math.Atan2(p1.Y - center.Y, p1.X - center.X);
            double aClick = Math.Atan2(click.Y - center.Y, click.X - center.X);

            a0 = a0 < 0 ? a0 + 2 * Math.PI : a0;
            a1 = a1 < 0 ? a1 + 2 * Math.PI : a1;
            aClick = aClick < 0 ? aClick + 2 * Math.PI : aClick;

            double sweepA = a1 - a0;
            if (sweepA < 0) sweepA += 2 * Math.PI;

            double clickDeltaA = aClick - a0;
            if (clickDeltaA < 0) clickDeltaA += 2 * Math.PI;
            bool keepArcA = clickDeltaA <= sweepA;

            Point start, end;
            double keptSweep;
            if (keepArcA)
            {
                start = p0; end = p1; keptSweep = sweepA;
            }
            else
            {
                start = p1; end = p0; keptSweep = 2 * Math.PI - sweepA;
            }

            bool large = keptSweep > Math.PI;
            SweepDirection dir = SweepDirection.Counterclockwise;

            canvas.Children.Remove(circle);
            DrawArcSegment(start, end, r, large, dir, circle.Stroke, circle.StrokeThickness);
        }

        private void TrimLineByCircle(Ellipse cutter, Line target, Point click)
        {
            var pts = GetLineCircleIntersection(target, cutter);
            if (pts.Count == 0) return;

            Point p1 = new(target.X1, target.Y1);
            Point p2 = new(target.X2, target.Y2);

            if (pts.Count == 1)
            {
                if (Distance(click, p2) < Distance(click, p1))
                {
                    target.X2 = pts[0].X; target.Y2 = pts[0].Y;
                }
                else
                    target.X1 = pts[0].X; target.Y1 = pts[0].Y;
                return;
            }

            double t1 = GetParameter(p1, p2, pts[0]);
            double t2 = GetParameter(p1, p2, pts[1]);
            if (t1 > t2) { var temp = pts[0]; pts[0] = pts[1]; pts[1] = temp; }

            if (Distance(click, p2) < Distance(click, p1))
            { target.X2 = pts[1].X; target.Y2 = pts[1].Y; }
            else
            { target.X1 = pts[0].X; target.Y1 = pts[0].Y; }
        }

        private double GetParameter(Point a, Point b, Point p)
        {
            double dx = b.X - a.X;
            double dy = b.Y - a.Y;
            if (Math.Abs(dx) > Math.Abs(dy)) return (p.X - a.X) / dx;
            else return (p.Y - a.Y) / dy;
        }
        private void TrimArcByLine(Line cutter, ShapePath arc, Point click)
        {
            var (fig, seg) = GetArcSegments(arc);
            if (fig == null || seg == null) return;

            Point originalStart = fig.StartPoint;
            Point originalEnd = seg.Point;
            double radius = seg.Size.Width;
            bool isLargeArc = seg.IsLargeArc;
            SweepDirection sweepDir = seg.SweepDirection;
            Brush stroke = arc.Stroke;
            double thickness = arc.StrokeThickness;

            var pts = GetLineArcIntersection(cutter, arc);
            System.Diagnostics.Debug.WriteLine($">>> TrimArcByLine: Initial intersections found = {pts.Count}");
            if (pts.Count == 0)
            {
                Vector dir = new Vector(cutter.X2 - cutter.X1, cutter.Y2 - cutter.Y1);
                if (dir.Length > 0.0001)
                {
                    dir.Normalize();
                    Point extendedEnd = new Point(cutter.X2 + dir.X * 5, cutter.Y2 + dir.Y * 5);
                    Line tempLine = new Line { X1 = cutter.X1, Y1 = cutter.Y1, X2 = extendedEnd.X, Y2 = extendedEnd.Y };
                    pts = GetLineArcIntersection(tempLine, arc);
                    System.Diagnostics.Debug.WriteLine($">>> TrimArcByLine: After 5px extension, intersections found = {pts.Count}");
                }
            }

            if (pts.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine($">>> TrimArcByLine: No touch detected (Real visible gap). Trim canceled.");
                return;
            }

            Point hit = pts.OrderBy(p => Distance(p, click)).First();
            bool clickOnStart = Distance(originalStart, click) < Distance(originalEnd, click);
            System.Diagnostics.Debug.WriteLine($">>> TrimArcByLine: Clicked side = {(clickOnStart ? "Start" : "End")}. Snapping to point ({hit.X}, {hit.Y})");

            Point newStart = clickOnStart ? hit : originalStart;
            Point newEnd = clickOnStart ? originalEnd : hit;

            canvas.Children.Remove(arc);
            DrawArcSegment(newStart, newEnd, radius, isLargeArc, sweepDir, stroke, thickness);
        }
        private void TrimLineByArc(ShapePath arc, Line target, Point click)
        {
            var pts = GetLineArcIntersection(target, arc);
            if (pts.Count == 0) return;
            Point hit = pts.OrderBy(p => Distance(p, click)).First();
            Point start = new(target.X1, target.Y1), end = new(target.X2, target.Y2);
            if (Distance(click, start) < Distance(click, end))
            { target.X1 = hit.X; target.Y1 = hit.Y; }
            else
            { target.X2 = hit.X; target.Y2 = hit.Y; }
        }

        private void TrimCircleByArc(ShapePath arc, Ellipse circle, Point click)
        {
            var pts = GetArcCircleIntersection(arc, circle);
            if (pts.Count < 2) return;

            Point center = GetCircleCenter(circle);
            double r = circle.Width / 2;

            var sorted = pts.OrderBy(p => Math.Atan2(p.Y - center.Y, p.X - center.X)).ToList();
            Point p0 = sorted[0], p1 = sorted[1];
            double a0 = Math.Atan2(p0.Y - center.Y, p0.X - center.X);
            double a1 = Math.Atan2(p1.Y - center.Y, p1.X - center.X);
            double aClick = Math.Atan2(click.Y - center.Y, click.X - center.X);
            a0 = a0 < 0 ? a0 + 2 * Math.PI : a0;
            a1 = a1 < 0 ? a1 + 2 * Math.PI : a1;
            aClick = aClick < 0 ? aClick + 2 * Math.PI : aClick;

            double sweep = a1 - a0; if (sweep < 0) sweep += 2 * Math.PI;
            double clickSweep = aClick - a0; if (clickSweep < 0) clickSweep += 2 * Math.PI;
            bool clickOnFirst = clickSweep <= sweep;

            Point start = clickOnFirst ? p1 : p0;
            Point end = clickOnFirst ? p0 : p1;
            double startAngle = Math.Atan2(start.Y - center.Y, start.X - center.X);
            double endAngle = Math.Atan2(end.Y - center.Y, end.X - center.X);
            startAngle = startAngle < 0 ? startAngle + 2 * Math.PI : startAngle;
            endAngle = endAngle < 0 ? endAngle + 2 * Math.PI : endAngle;
            double keptSweep = endAngle - startAngle; if (keptSweep < 0) keptSweep += 2 * Math.PI;
            bool large = keptSweep > Math.PI;
            SweepDirection dir = clickOnFirst ? SweepDirection.Clockwise : SweepDirection.Counterclockwise;

            canvas.Children.Remove(circle);
            DrawArcSegment(start, end, r, large, dir, circle.Stroke, circle.StrokeThickness);
        }

        private void TrimArcByCircle(Ellipse circle, ShapePath arc, Point clickPoint)
        {
            var (fig, seg) = GetArcSegments(arc);
            if (fig == null || seg == null) return;

            bool clickedStart = Distance(fig.StartPoint, clickPoint) < Distance(seg.Point, clickPoint);
            Point targetPoint = clickedStart ? fig.StartPoint : seg.Point;

            Point center = GetCircleCenter(circle);
            double radius = circle.Width / 2;

            var pts = GetArcCircleIntersection(arc, circle);
            if (pts.Count > 0)
            {
                Point hit = pts.OrderBy(p => Distance(p, clickPoint)).First();
                if (clickedStart) fig.StartPoint = hit;
                else seg.Point = hit;
                return;
            }

            Vector direction = targetPoint - center;
            if (direction.Length < 0.0001) direction = new Vector(0, -1);
            direction.Normalize();

            Point nearestPointOnCircle = center + (direction * radius);
            if (clickedStart) fig.StartPoint = nearestPointOnCircle;
            else seg.Point = nearestPointOnCircle;
        }

        private void TrimCircleByCircle(Ellipse cutter, Ellipse target, Point click)
        {
            var pts = GetCircleCircleIntersection(cutter, target);
            if (pts.Count < 2) return;

            Point center = GetCircleCenter(target);
            double r = target.Width / 2;

            var sorted = pts.OrderBy(p => Math.Atan2(p.Y - center.Y, p.X - center.X)).ToList();
            Point p0 = sorted[0], p1 = sorted[1];
            double a0 = Math.Atan2(p0.Y - center.Y, p0.X - center.X);
            double a1 = Math.Atan2(p1.Y - center.Y, p1.X - center.X);
            double aClick = Math.Atan2(click.Y - center.Y, click.X - center.X);
            a0 = a0 < 0 ? a0 + 2 * Math.PI : a0;
            a1 = a1 < 0 ? a1 + 2 * Math.PI : a1;
            aClick = aClick < 0 ? aClick + 2 * Math.PI : aClick;

            double sweep = a1 - a0; if (sweep < 0) sweep += 2 * Math.PI;
            double clickSweep = aClick - a0; if (clickSweep < 0) clickSweep += 2 * Math.PI;
            bool clickOnFirst = clickSweep <= sweep;

            Point start = clickOnFirst ? p1 : p0;
            Point end = clickOnFirst ? p0 : p1;
            double startAngle = Math.Atan2(start.Y - center.Y, start.X - center.X);
            double endAngle = Math.Atan2(end.Y - center.Y, end.X - center.X);
            startAngle = startAngle < 0 ? startAngle + 2 * Math.PI : startAngle;
            endAngle = endAngle < 0 ? endAngle + 2 * Math.PI : endAngle;
            double keptSweep = endAngle - startAngle; if (keptSweep < 0) keptSweep += 2 * Math.PI;
            bool large = keptSweep > Math.PI;
            SweepDirection dir = clickOnFirst ? SweepDirection.Clockwise : SweepDirection.Counterclockwise;

            canvas.Children.Remove(target);
            DrawArcSegment(start, end, r, large, dir, target.Stroke, target.StrokeThickness);
        }

        public void ExtendLine(Line line, Shape boundary, Point clickPoint)
        {
            Point tip = new(line.X1, line.Y1);
            List<Point> pts = new();

            if (boundary is ShapePath arc)
            {
                var (fig, seg) = GetArcSegments(arc);
                if (fig == null || seg == null) return;

                double radius = seg.Size.Width;
                Point center = GetArcCenter(fig.StartPoint, seg.Point, radius, seg.IsLargeArc, seg.SweepDirection);

                Ellipse tempCircle = new Ellipse { Width = radius * 2, Height = radius * 2 };
                Canvas.SetLeft(tempCircle, center.X - radius);
                Canvas.SetTop(tempCircle, center.Y - radius);

                pts = GetLineCircleIntersectionInfinite(line, tempCircle);
            }
            else if (boundary is Line bLine)
            {
                if (GetLineLineIntersection(line, bLine, out Point hit))
                {
                    if (Distance(new(line.X1, line.Y1), clickPoint) < Distance(new(line.X2, line.Y2), clickPoint))
                    {
                        line.X1 = hit.X; line.Y1 = hit.Y; 
                    }
                    else
                    {
                        line.X2 = hit.X; line.Y2 = hit.Y; 
                    }
                    return;
                }
            }
            else if (boundary is Ellipse circle)
            {
                pts = GetLineCircleIntersectionInfinite(line, circle);
            }

            if (pts.Count > 0)
            {
                Point hit = pts.OrderBy(p => Distance(p, tip)).First();
                line.X1 = hit.X; line.Y1 = hit.Y;
            }
        }

        public void ExtendArc(ShapePath arc, Shape boundary)
        {
            var (fig, seg) = GetArcSegments(arc);
            if (fig == null || seg == null) return;

            List<Point> pts = new();

            if (boundary is Line line)
            {
                Point center = GetArcCenter(fig.StartPoint, seg.Point, seg.Size.Width, seg.IsLargeArc, seg.SweepDirection);
                double radius = seg.Size.Width;

                Ellipse tempCircle = new Ellipse { Width = radius * 2, Height = radius * 2 };
                Canvas.SetLeft(tempCircle, center.X - radius);
                Canvas.SetTop(tempCircle, center.Y - radius);

                pts = GetLineCircleIntersectionInfinite(line, tempCircle);
            }
            else if (boundary is Ellipse circle)
            {
                pts = GetArcCircleIntersection(arc, circle);
            }
            else if (boundary is ShapePath arcBoundary)
            {
                pts = GetArcArcIntersection(arc, arcBoundary);
            }
            else
            {
                return;
            }

            if (pts.Count == 0) return;

            Point endPoint = seg.Point;
            Point hit = pts.OrderBy(p => Distance(p, endPoint)).First();
            seg.Point = hit;
        }

        public void Save()
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "CAD Drawing (*.json)|*.json",
                DefaultExt = "json",
                Title = "Save Drawing"
            };
            if (saveDialog.ShowDialog() != true) return;

            var list = new List<ShapeData>();
            foreach (UIElement child in canvas.Children)
            {
                if (child is Line l)
                    list.Add(new ShapeData { Type = "Line", X1 = l.X1, Y1 = l.Y1, X2 = l.X2, Y2 = l.Y2 });
                else if (child is Ellipse e)
                {
                    double left = Canvas.GetLeft(e), top = Canvas.GetTop(e);
                    list.Add(new ShapeData { Type = "Circle", X1 = left, Y1 = top, Width = e.Width, Height = e.Height });
                }
                else if (child is ShapePath p && p.Data is PathGeometry geo && geo.Figures.Count > 0)
                {
                    var fig = geo.Figures[0];
                    var seg = fig.Segments.OfType<ArcSegment>().FirstOrDefault();
                    if (seg != null)
                        list.Add(new ShapeData
                        {
                            Type = "Arc",
                            X1 = fig.StartPoint.X,
                            Y1 = fig.StartPoint.Y,
                            X2 = seg.Point.X,
                            Y2 = seg.Point.Y,
                            Width = seg.Size.Width,
                            Height = seg.Size.Height
                        });
                }
            }

            string json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(saveDialog.FileName, json);
            MessageBox.Show("Saved successfully.", "Success");
        }

        public void Open()
        {
            var openDialog = new OpenFileDialog
            {
                Filter = "CAD Drawing (*.json)|*.json",
                Title = "Open Drawing"
            };
            if (openDialog.ShowDialog() != true) return;

            try
            {
                string json = File.ReadAllText(openDialog.FileName);
                var list = JsonSerializer.Deserialize<List<ShapeData>>(json);
                if (list == null) return;

                canvas.Children.Clear();
                foreach (var data in list)
                {
                    if (data.Type == "Line")
                        DrawLine(new(data.X1, data.Y1), new(data.X2, data.Y2));
                    else if (data.Type == "Circle")
                        DrawCircle(new(data.X1 + data.Width / 2, data.Y1 + data.Height / 2), data.Width / 2);
                    else if (data.Type == "Arc")
                    {
                        double r = data.Width / 2;
                        DrawArcSegment(new(data.X1, data.Y1), new(data.X2, data.Y2), r, false, SweepDirection.Clockwise);
                    }
                }
                MessageBox.Show("Loaded successfully.", "Success");
            }
            catch (Exception ex) { MessageBox.Show("Error loading: " + ex.Message, "Error"); }
        }

        private double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
        private Point GetCircleCenter(Ellipse c) => new(Canvas.GetLeft(c) + c.Width / 2, Canvas.GetTop(c) + c.Height / 2);

        private (PathFigure?, ArcSegment?) GetArcSegments(ShapePath arc)
        {
            if (arc.Data is not PathGeometry geo || geo.Figures.Count == 0) return (null, null);
            var fig = geo.Figures[0];
            var seg = fig.Segments.OfType<ArcSegment>().FirstOrDefault();
            return (fig, seg);
        }

        private Point GetArcCenter(Point start, Point end, double radius, bool isLargeArc, SweepDirection sweepDirection)
        {
            double d = Distance(start, end);
            if (d > 2 * radius || d == 0) return new Point(0, 0);
            double h = Math.Sqrt(radius * radius - d * d / 4);
            Point mid = new((start.X + end.X) / 2, (start.Y + end.Y) / 2);
            double factor = sweepDirection == SweepDirection.Clockwise ? 1 : -1;
            if (isLargeArc) factor *= -1;
            return new Point(
                mid.X + factor * h * (start.Y - end.Y) / d,
                mid.Y - factor * h * (start.X - end.X) / d
            );
        }

        private bool GetLineLineIntersection(Line a, Line b, out Point hit)
        {
            hit = new Point();
            double d = (a.X1 - a.X2) * (b.Y1 - b.Y2) - (a.Y1 - a.Y2) * (b.X1 - b.X2);
            if (Math.Abs(d) < 1e-6) return false;
            double x = ((a.X1 * a.Y2 - a.Y1 * a.X2) * (b.X1 - b.X2) - (a.X1 - a.X2) * (b.X1 * b.Y2 - b.Y1 * b.X2)) / d;
            double y = ((a.X1 * a.Y2 - a.Y1 * a.X2) * (b.Y1 - b.Y2) - (a.Y1 - a.Y2) * (b.X1 * b.Y2 - b.Y1 * b.X2)) / d;
            hit = new Point(x, y);
            return true;
        }

        private List<Point> GetLineCircleIntersection(Line line, Ellipse circle)
        {
            var result = new List<Point>();
            Point c = GetCircleCenter(circle);
            double r = circle.Width / 2;
            double dx = line.X2 - line.X1, dy = line.Y2 - line.Y1;
            if (dx == 0 && dy == 0) return result;

            double a = dx * dx + dy * dy;
            double b = 2 * (dx * (line.X1 - c.X) + dy * (line.Y1 - c.Y));
            double cVal = (line.X1 - c.X) * (line.X1 - c.X) + (line.Y1 - c.Y) * (line.Y1 - c.Y) - r * r;
            double disc = b * b - 4 * a * cVal;
            if (disc < 0) return result;

            double t1 = (-b + Math.Sqrt(disc)) / (2 * a);
            double t2 = (-b - Math.Sqrt(disc)) / (2 * a);

            if (t1 >= 0 && t1 <= 1) result.Add(new(line.X1 + t1 * dx, line.Y1 + t1 * dy));
            if (t2 >= 0 && t2 <= 1 && Math.Abs(t1 - t2) > 1e-9) result.Add(new(line.X1 + t2 * dx, line.Y1 + t2 * dy));
            return result;
        }

        private List<Point> GetLineArcIntersection(Line line, ShapePath arc, bool filterByArcBounds = true)
        {
            var result = new List<Point>();
            var (fig, seg) = GetArcSegments(arc);
            if (fig == null || seg == null) return result;

            Point p1 = fig.StartPoint, p2 = seg.Point;
            double r = seg.Size.Width;
            double d = Distance(p1, p2);
            if (d > 2 * r || d == 0) return result;

            double h = Math.Sqrt(r * r - d * d / 4);
            Point mid = new((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2);
            double factor = seg.SweepDirection == SweepDirection.Clockwise ? 1 : -1;
            if (seg.IsLargeArc) factor *= -1;
            Point center = new(
                mid.X + factor * h * (p1.Y - p2.Y) / d,
                mid.Y - factor * h * (p1.X - p2.X) / d
            ); 

            var tempCircle = new Ellipse { Width = r * 2, Height = r * 2 };
            Canvas.SetLeft(tempCircle, center.X - r);
            Canvas.SetTop(tempCircle, center.Y - r);
            var candidates = GetLineCircleIntersection(line, tempCircle);
            if (!filterByArcBounds) return candidates;

            double aStart = Math.Atan2(p1.Y - center.Y, p1.X - center.X);
            double aEnd = Math.Atan2(p2.Y - center.Y, p2.X - center.X);
            bool cw = seg.SweepDirection == SweepDirection.Clockwise;
            foreach (var p in candidates)
            {
                double a = Math.Atan2(p.Y - center.Y, p.X - center.X);
                if (IsAngleBetween(a, aStart, aEnd, cw)) result.Add(p);
            }
            return result;
        }

        private List<Point> GetArcCircleIntersection(ShapePath arc, Ellipse circle)
        {
            var result = new List<Point>();
            var (fig, seg) = GetArcSegments(arc);
            if (fig == null || seg == null) return result;

            Point c1 = GetCircleCenter(circle);
            double r1 = circle.Width / 2;
            Point p1 = fig.StartPoint, p2 = seg.Point;
            double r2 = seg.Size.Width;
            double d = Distance(p1, p2);
            if (d > 2 * r2 || d == 0) return result;

            double h = Math.Sqrt(r2 * r2 - d * d / 4);
            Point mid = new((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2);
            double factor = seg.SweepDirection == SweepDirection.Clockwise ? 1 : -1;
            if (seg.IsLargeArc) factor *= -1;
            Point c2 = new(
                mid.X + factor * h * (p1.Y - p2.Y) / d,
                mid.Y - factor * h * (p1.X - p2.X) / d
            );

            double D = Distance(c1, c2);
            if (D > r1 + r2 || D < Math.Abs(r1 - r2) || D == 0) return result;

            double A = (r1 * r1 - r2 * r2 + D * D) / (2 * D);
            double H = Math.Sqrt(r1 * r1 - A * A);
            Point P2 = new(c1.X + A * (c2.X - c1.X) / D, c1.Y + A * (c2.Y - c1.Y) / D);
            Point pt1 = new(P2.X + H * (c2.Y - c1.Y) / D, P2.Y - H * (c2.X - c1.X) / D);
            Point pt2 = new(P2.X - H * (c2.Y - c1.Y) / D, P2.Y + H * (c2.X - c1.X) / D);

            double aStart = Math.Atan2(p1.Y - c2.Y, p1.X - c2.X);
            double aEnd = Math.Atan2(p2.Y - c2.Y, p2.X - c2.X);
            bool cw = seg.SweepDirection == SweepDirection.Clockwise;
            foreach (var pt in new[] { pt1, pt2 })
            {
                double a = Math.Atan2(pt.Y - c2.Y, pt.X - c2.X);
                if (IsAngleBetween(a, aStart, aEnd, cw)) result.Add(pt);
            }
            return result;
        }

        private List<Point> GetCircleCircleIntersection(Ellipse c1, Ellipse c2)
        {
            var result = new List<Point>();
            Point p1 = GetCircleCenter(c1), p2 = GetCircleCenter(c2);
            double r1 = c1.Width / 2, r2 = c2.Width / 2;
            double d = Distance(p1, p2);
            if (d > r1 + r2 || d < Math.Abs(r1 - r2) || d == 0) return result;

            double a = (r1 * r1 - r2 * r2 + d * d) / (2 * d);
            double h = Math.Sqrt(r1 * r1 - a * a);
            Point p = new(p1.X + a * (p2.X - p1.X) / d, p1.Y + a * (p2.Y - p1.Y) / d);
            result.Add(new(p.X + h * (p2.Y - p1.Y) / d, p.Y - h * (p2.X - p1.X) / d));
            result.Add(new(p.X - h * (p2.Y - p1.Y) / d, p.Y + h * (p2.X - p1.X) / d));
            return result;
        }

        private List<Point> GetArcArcIntersection(ShapePath arc1, ShapePath arc2)
        {
            var result = new List<Point>();
            var (fig1, seg1) = GetArcSegments(arc1);
            var (fig2, seg2) = GetArcSegments(arc2);
            if (fig1 == null || seg1 == null || fig2 == null || seg2 == null) return result;

            Point p1_start = fig1.StartPoint, p1_end = seg1.Point;
            double r1 = seg1.Size.Width;
            double d1 = Distance(p1_start, p1_end);
            if (d1 > 2 * r1 + 1e-6 || d1 == 0) return result;
            double h1 = Math.Sqrt(r1 * r1 - d1 * d1 / 4);
            Point mid1 = new((p1_start.X + p1_end.X) / 2, (p1_start.Y + p1_end.Y) / 2);
            double factor1 = seg1.SweepDirection == SweepDirection.Clockwise ? 1 : -1;
            if (seg1.IsLargeArc) factor1 *= -1;
            Point center1 = new(mid1.X + factor1 * h1 * (p1_start.Y - p1_end.Y) / d1, mid1.Y - factor1 * h1 * (p1_start.X - p1_end.X) / d1);

            Point p2_start = fig2.StartPoint, p2_end = seg2.Point;
            double r2 = seg2.Size.Width;
            double d2 = Distance(p2_start, p2_end);
            if (d2 > 2 * r2 + 1e-6 || d2 == 0) return result;
            double h2 = Math.Sqrt(r2 * r2 - d2 * d2 / 4);
            Point mid2 = new((p2_start.X + p2_end.X) / 2, (p2_start.Y + p2_end.Y) / 2);
            double factor2 = seg2.SweepDirection == SweepDirection.Clockwise ? 1 : -1;
            if (seg2.IsLargeArc) factor2 *= -1;
            Point center2 = new(mid2.X + factor2 * h2 * (p2_start.Y - p2_end.Y) / d2, mid2.Y - factor2 * h2 * (p2_start.X - p2_end.X) / d2);

            var fakeCircle1 = new Ellipse { Width = r1 * 2, Height = r1 * 2 };
            Canvas.SetLeft(fakeCircle1, center1.X - r1);
            Canvas.SetTop(fakeCircle1, center1.Y - r1);
            var fakeCircle2 = new Ellipse { Width = r2 * 2, Height = r2 * 2 };
            Canvas.SetLeft(fakeCircle2, center2.X - r2);
            Canvas.SetTop(fakeCircle2, center2.Y - r2);

            var candidates = GetCircleCircleIntersection(fakeCircle1, fakeCircle2);

            double a1_start = Math.Atan2(p1_start.Y - center1.Y, p1_start.X - center1.X);
            double a1_end = Math.Atan2(p1_end.Y - center1.Y, p1_end.X - center1.X);
            bool cw1 = seg1.SweepDirection == SweepDirection.Clockwise;

            double a2_start = Math.Atan2(p2_start.Y - center2.Y, p2_start.X - center2.X);
            double a2_end = Math.Atan2(p2_end.Y - center2.Y, p2_end.X - center2.X);
            bool cw2 = seg2.SweepDirection == SweepDirection.Clockwise;

            foreach (var pt in candidates)
            {
                double a1 = Math.Atan2(pt.Y - center1.Y, pt.X - center1.X);
                double a2 = Math.Atan2(pt.Y - center2.Y, pt.X - center2.X);
                if (IsAngleBetween(a1, a1_start, a1_end, cw1) && IsAngleBetween(a2, a2_start, a2_end, cw2))
                    result.Add(pt);
            }
            return result;
        }

        private List<Point> GetLineCircleIntersectionInfinite(Line line, Ellipse circle)
        {
            var result = new List<Point>();
            Point c = GetCircleCenter(circle);
            double r = circle.Width / 2;
            double dx = line.X2 - line.X1, dy = line.Y2 - line.Y1;
            if (dx == 0 && dy == 0) return result;

            double a = dx * dx + dy * dy;
            double b = 2 * (dx * (line.X1 - c.X) + dy * (line.Y1 - c.Y));
            double cVal = (line.X1 - c.X) * (line.X1 - c.X) + (line.Y1 - c.Y) * (line.Y1 - c.Y) - r * r;
            double disc = b * b - 4 * a * cVal;
            if (disc < 0) return result;

            double t1 = (-b + Math.Sqrt(disc)) / (2 * a);
            double t2 = (-b - Math.Sqrt(disc)) / (2 * a);

            result.Add(new(line.X1 + t1 * dx, line.Y1 + t1 * dy));
            if (Math.Abs(t1 - t2) > 1e-9) result.Add(new(line.X1 + t2 * dx, line.Y1 + t2 * dy));
            return result;
        }

        private bool IsAngleBetween(double target, double start, double end, bool clockwise)
        {
            target = (target + 2 * Math.PI) % (2 * Math.PI);
            start = (start + 2 * Math.PI) % (2 * Math.PI);
            end = (end + 2 * Math.PI) % (2 * Math.PI);
            if (clockwise)
            {
                if (start <= end) return target >= start && target <= end;
                return target >= start || target <= end;
            }
            else
            {
                if (end <= start) return target >= end && target <= start;
                return target >= end || target <= start;
            }
        }

        public void DrawArcSegment(Point start, Point end, double radius, bool isLargeArc, SweepDirection sweepDirection, Brush stroke = null, double thickness = 1.0)
        {
            var fig = new PathFigure { StartPoint = start, IsClosed = false };
            var seg = new ArcSegment
            {
                Point = end,
                Size = new Size(radius, radius),
                IsLargeArc = isLargeArc,
                SweepDirection = sweepDirection,
                IsStroked = true
            };
            fig.Segments.Add(seg);
            var geo = new PathGeometry();
            geo.Figures.Add(fig);
            var path = new ShapePath
            {
                Data = geo,
                Stroke = stroke ?? Brushes.Yellow,
                StrokeThickness = thickness
            };
            canvas.Children.Add(path);
        }

        private class ShapeData
        {
            public string Type { get; set; } = "";
            public double X1 { get; set; }
            public double Y1 { get; set; }
            public double X2 { get; set; }
            public double Y2 { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
        }
    }
}