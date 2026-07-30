using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ShapePath = System.Windows.Shapes.Path;

namespace Project
{
    public partial class MainWindow : Window
    {
        private MainViewModel vm;
        private DrawingService drawingService;
             
        private bool waitingForLineFirstPoint = false;
        private bool waitingForLineSecondPoint = false;
        private Point lineFirstPoint;

        private Point circleCenter;
        private bool waitingForCircleCenter = false;
        private bool isDrawingCirclePreview = false;
        private Ellipse? circlePreview;

        private int arcStep = 0;
        private Point[] arcPoints = new Point[3];
                
        private bool isMoving = false;
        private Point moveStartPoint;
        private Shape? selectedShape;

        private int trimStep = 0;
        private Shape? cuttingShape;
        private Shape? trimShape;

        private Shape? extendBoundary;

        private const double SelectionTolerance = 10.0;

        public MainWindow()
        {
            InitializeComponent();
            drawingService = new DrawingService(canvas);
            vm = new MainViewModel(drawingService);
            DataContext = vm;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) => vm.Prompt = "Ready";

        private void LineButton_Click(object sender, RoutedEventArgs e)
        {
            vm.CurrentTool = "Line";
            waitingForLineFirstPoint = true;
            waitingForLineSecondPoint = false;
            vm.Prompt = "Click first point";
        }

        private void CircleButton_Click(object sender, RoutedEventArgs e)
        {
            vm.CurrentTool = "Circle";
            ResetCircleDrawing(); 
            waitingForCircleCenter = true;
            vm.Prompt = "Click center point";
        }

        private void ArcButton_Click(object sender, RoutedEventArgs e)
        {
            vm.CurrentTool = "Arc";
            arcStep = 0;
            vm.Prompt = "Click first point";
        }

        private void ThreePointArcButton_Click(object sender, RoutedEventArgs e)
        {
            vm.CurrentTool = "ThreePointArc";
            arcStep = 0;
            vm.Prompt = "Click first point";
        }

        private void ResetCircleDrawing()
        {
            isDrawingCirclePreview = false;
            waitingForCircleCenter = false;
            if (circlePreview != null && canvas.Children.Contains(circlePreview))
            {
                canvas.Children.Remove(circlePreview);
            }
            circlePreview = null;
        }
           
        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Point point = e.GetPosition(canvas);

            if (vm.CurrentTool == "Line")
            {
                if (waitingForLineFirstPoint)
                {
                    lineFirstPoint = point;
                    waitingForLineFirstPoint = false;
                    waitingForLineSecondPoint = true;
                    vm.Prompt = "Click second point (orthogonal)";
                    return;
                }
                if (waitingForLineSecondPoint)
                {
                    double dx = Math.Abs(point.X - lineFirstPoint.X);
                    double dy = Math.Abs(point.Y - lineFirstPoint.Y);
                    Point end = dx > dy ? new Point(point.X, lineFirstPoint.Y) : new Point(lineFirstPoint.X, point.Y);
                    drawingService.DrawLine(lineFirstPoint, end);
                    waitingForLineSecondPoint = false;
                    vm.Prompt = "Ready";
                    return;
                }
            }

            else if (vm.CurrentTool == "Circle")
            {
                if (waitingForCircleCenter)
                {
                    circleCenter = point;
                    waitingForCircleCenter = false;
                    isDrawingCirclePreview = true;

                    circlePreview = new Ellipse
                    {
                        Stroke = Brushes.Yellow,
                        StrokeThickness = 1,
                        StrokeDashArray = new DoubleCollection { 4, 4 } 
                    };
                    Canvas.SetLeft(circlePreview, point.X);
                    Canvas.SetTop(circlePreview, point.Y);
                    canvas.Children.Add(circlePreview);

                    vm.Prompt = "Move mouse to set radius and click, or type radius in command box";
                    CommandBox.Focus();
                    return;
                }

                if (isDrawingCirclePreview && circlePreview != null)
                {
                    double radius = Distance(circleCenter, point);
                    if (radius > 0)
                    {
                        canvas.Children.Remove(circlePreview);
                        drawingService.DrawCircle(circleCenter, radius);
                        ResetCircleDrawing();
                        vm.Prompt = "Ready";
                    }
                    return;
                }
            }

            else if (vm.CurrentTool == "Arc" || vm.CurrentTool == "ThreePointArc")
            {
                arcPoints[arcStep] = point;
                arcStep++;
                if (arcStep == 3)
                {
                    var arc = DrawThreePointArc(arcPoints[0], arcPoints[1], arcPoints[2]);
                    if (arc != null)
                        canvas.Children.Add(arc);
                    else
                        vm.Prompt = "Points are collinear – try again";
                    arcStep = 0;
                    vm.Prompt = "Ready";
                }
                else
                {
                    vm.Prompt = $"Click point {arcStep + 1}";
                }
                return;
            }

            else if (vm.CurrentTool == "Move")
            {
                selectedShape = drawingService.SelectShape(point);
                if (selectedShape != null)
                {
                    moveStartPoint = point;
                    isMoving = true;
                    vm.Prompt = "Moving object";
                }
                else
                {
                    vm.Prompt = "No object under cursor";
                }
                return;
            }
            else if (vm.CurrentTool == "Trim")
            {
                Shape? shape = drawingService.SelectShape(point);
                if (shape == null) { vm.Prompt = "No shape found"; return; }

                if (trimStep == 0)
                {
                    cuttingShape = shape;
                    trimStep = 1;
                    vm.Prompt = "Select object to trim";
                }
                else
                {
                    trimShape = shape;
                    if (cuttingShape != null && trimShape != null)
                        drawingService.Trim(cuttingShape, trimShape, point);
                    trimStep = 0;
                    cuttingShape = null;
                    trimShape = null;
                    vm.Prompt = "Ready";
                }
                return;
            }
            else if (vm.CurrentTool == "Delete")
            {
                Shape? shape = drawingService.SelectShape(point);
                if (shape != null)
                {
                    drawingService.DeleteShape(shape);
                    vm.Prompt = "Object deleted";
                }
                else
                    vm.Prompt = "No shape found";
                return;
            }
            else if (vm.CurrentTool == "Extend")
            {
                Shape? shape = drawingService.SelectShape(point);
                if (shape == null) { vm.Prompt = "No shape found"; return; }

                if (extendBoundary == null)
                {
                    extendBoundary = shape;
                    extendBoundary.StrokeDashArray = new DoubleCollection { 4, 4 };
                    vm.Prompt = "Boundary locked! Click the object to extend";
                }
                else
                {
                    if (shape is Line lineToExtend)
                        drawingService.ExtendLine(lineToExtend, extendBoundary, point);
                    else if (shape is ShapePath arcToExtend)
                        drawingService.ExtendArc(arcToExtend, extendBoundary);
                    else
                    {
                        vm.Prompt = "Can only extend Line or Arc";
                        return;
                    }
                    extendBoundary.StrokeDashArray = null;
                    extendBoundary = null;
                    vm.Prompt = "Ready";
                }
                return;
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            Point current = e.GetPosition(canvas);

            XCoordinate.Text = ((int)current.X).ToString();
            YCoordinate.Text = ((int)current.Y).ToString();

            if (vm.CurrentTool == "Move" && isMoving && selectedShape != null)
            {
                drawingService.MoveShape(selectedShape, moveStartPoint, current);
                moveStartPoint = current;
            }

            if (vm.CurrentTool == "Circle" && isDrawingCirclePreview && circlePreview != null)
            {
                double radius = Distance(circleCenter, current);
                double diameter = radius * 2;
                circlePreview.Width = diameter;
                circlePreview.Height = diameter;
                Canvas.SetLeft(circlePreview, circleCenter.X - radius);
                Canvas.SetTop(circlePreview, circleCenter.Y - radius);

                vm.Prompt = $"Radius: {radius:F1} -(Click to place or type exact radius)";
            }
        }

        private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (vm.CurrentTool == "Move")
            {
                isMoving = false;
                selectedShape = null;
                vm.Prompt = "Ready";
            }
        }

        private void CommandBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            string input = CommandBox.Text.Trim().ToUpper();
            if (string.IsNullOrEmpty(input)) return;

            if (vm.CurrentTool == "Circle" && isDrawingCirclePreview && circlePreview != null)
            {
                if (double.TryParse(input, out double radius) && radius > 0)
                {
                    canvas.Children.Remove(circlePreview);
                    drawingService.DrawCircle(circleCenter, radius);
                    ResetCircleDrawing();
                    CommandBox.Clear();
                    canvas.Focus();
                    vm.Prompt = "Ready";
                }
                else
                {
                    MessageBox.Show("Enter a valid number.");
                }
                return;
            }

            switch (input)
            {
                case "LINE": case "L": LineButton_Click(null, null); break;
                case "CIRCLE": case "C": CircleButton_Click(null, null); break;
                case "ARC": case "A": ArcButton_Click(null, null); break;
                case "3PARC": ThreePointArcButton_Click(null, null); break;
                case "MOVE": case "M": vm.MoveCommand.Execute(null); break;
                case "TRIM": case "TR": vm.TrimCommand.Execute(null); break;
                case "EXTEND": case "EX": vm.ExtendCommand.Execute(null); break;
                case "DELETE": case "D": vm.DeleteCommand.Execute(null); break;
                case "SAVE": vm.SaveCommand.Execute(null); break;
                case "OPEN": vm.OpenCommand.Execute(null); break;
                case "DELETEALL": vm.DeleteAllCommand.Execute(null); break;
                default: MessageBox.Show("Unknown command."); break;
            }
            CommandBox.Clear();
        }
        private double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    public ShapePath DrawThreePointArc(Point p1, Point p2, Point p3, Brush stroke = null, double thickness = 2.0)
        {
            double temp = p2.X * p2.X + p2.Y * p2.Y;
            double bc = (p1.X * p1.X + p1.Y * p1.Y - temp) / 2.0;
            double cd = (temp - p3.X * p3.X - p3.Y * p3.Y) / 2.0;
            double det = (p1.X - p2.X) * (p2.Y - p3.Y) - (p2.X - p3.X) * (p1.Y - p2.Y);
            if (Math.Abs(det) < 1e-6) return null;

            double cx = (bc * (p2.Y - p3.Y) - cd * (p1.Y - p2.Y)) / det;
            double cy = ((p1.X - p2.X) * cd - (p2.X - p3.X) * bc) / det;
            double radius = Math.Sqrt((p1.X - cx) * (p1.X - cx) + (p1.Y - cy) * (p1.Y - cy));

            double cross = (p2.X - p1.X) * (p3.Y - p1.Y) - (p2.Y - p1.Y) * (p3.X - p1.X);
            SweepDirection sweepDir = cross > 0 ? SweepDirection.Clockwise : SweepDirection.Counterclockwise;

            double a1 = Math.Atan2(p1.Y - cy, p1.X - cx); if (a1 < 0) a1 += 2 * Math.PI;
            double a2 = Math.Atan2(p2.Y - cy, p2.X - cx); if (a2 < 0) a2 += 2 * Math.PI;
            double a3 = Math.Atan2(p3.Y - cy, p3.X - cx); if (a3 < 0) a3 += 2 * Math.PI;

            double sweep12 = a2 - a1;
            double sweep23 = a3 - a2;
            if (sweepDir == SweepDirection.Clockwise)
            {
                if (sweep12 < 0) sweep12 += 2 * Math.PI;
                if (sweep23 < 0) sweep23 += 2 * Math.PI;
            }
            else
            {
                if (sweep12 > 0) sweep12 -= 2 * Math.PI;
                if (sweep23 > 0) sweep23 -= 2 * Math.PI;
            }
            double totalSweep = sweep12 + sweep23;
            bool isLarge = Math.Abs(totalSweep) > Math.PI;

            var arcSeg = new ArcSegment
            {
                Point = p3,
                Size = new Size(radius, radius),
                SweepDirection = sweepDir,
                IsLargeArc = isLarge,
                IsStroked = true
            };

            var fig = new PathFigure { StartPoint = p1, IsClosed = false };
            fig.Segments.Add(arcSeg);

            var geo = new PathGeometry();
            geo.Figures.Add(fig);

            return new ShapePath
            {
                Data = geo,
                Stroke = stroke ?? Brushes.Red,
                StrokeThickness = thickness
            };
        }
    }
}