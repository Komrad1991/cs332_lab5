using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using System.Drawing;
using System.Reflection.Metadata;

namespace FractalApp.Parts;

public sealed class BezierEditor : Control
{
    private readonly List<Point> _points = new();
    private int _selectedIndex = -1;
    private int _dragIndex = -1;

    private const double PointRadius = 7;
    private const double HitRadius = 14;

    public BezierEditor()
    {
        ClipToBounds = true;

        SizeChanged += (_, e) =>
        {
            if (_points.Count == 0 &&
                e.NewSize.Width > 100 &&
                e.NewSize.Height > 100)
            {
                ResetPoints();
            }
        };
    }

    public void ResetPoints()
    {
        _points.Clear();

        var width = Math.Max(Bounds.Width, 500);
        var height = Math.Max(Bounds.Height, 350);

        _points.Add(new Point(width * 0.10, height * 0.72));
        _points.Add(new Point(width * 0.35, height * 0.25));
        _points.Add(new Point(width * 0.62, height * 0.78));
        _points.Add(new Point(width * 0.90, height * 0.28));

        _selectedIndex = -1;
        _dragIndex = -1;
        InvalidateVisual();
    }

    public void AddPoint()
    {
        Point point;

        if (_points.Count == 0)
        {
            point = new Point(Bounds.Width / 2, Bounds.Height / 2);
        }
        else
        {
            var last = _points[^1];
            var dx = Bounds.Width > 300 ? 80 : 40;
            var dy = Random.Shared.NextDouble() * 80 - 40;

            point = ClampPoint(new Point(last.X + dx, last.Y + dy));
        }

        AddPointAt(point);
    }

    public void DeleteSelected()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _points.Count)
            return;

        if (_points.Count <= 2)
            return;

        _points.RemoveAt(_selectedIndex);
        _selectedIndex = -1;
        _dragIndex = -1;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        context.FillRectangle(
            Brushes.White,
            new Rect(0, 0, Bounds.Width, Bounds.Height));

        DrawGrid(context);

        if (_points.Count < 2)
            return;

        var curvePen = new Pen(
            new SolidColorBrush(Color.FromRgb(35, 105, 220)),
            2.6);

        var geometry = new StreamGeometry();

        using (var gc = geometry.Open())
        {
            gc.BeginFigure(_points[0], false);

            for (var i = 0; i < _points.Count - 1; i++)
            {
                var (c1, c2) = GetBezierControls(i);
                gc.CubicBezierTo(c1, c2, _points[i + 1]);
            }

            gc.EndFigure(false);
        }

        context.DrawGeometry(null, curvePen, geometry);

        // Касательные вычисляются автоматически; редактируются только опорные точки.
        var handlePen = new Pen(
            new SolidColorBrush(Color.FromArgb(140, 120, 120, 120)),
            1);

        var handleBrush = new SolidColorBrush(Color.FromArgb(80, 120, 120, 120));

        for (var i = 0; i < _points.Count - 1; i++)
        {
            var (c1, c2) = GetBezierControls(i);

            context.DrawLine(handlePen, _points[i], c1);
            context.DrawLine(handlePen, _points[i + 1], c2);

            context.DrawEllipse(handleBrush, handlePen, c1, 3, 3);
            context.DrawEllipse(handleBrush, handlePen, c2, 3, 3);
        }

        for (var i = 0; i < _points.Count; i++)
        {
            var selected = i == _selectedIndex;
            var fill = new SolidColorBrush(
                selected
                    ? Color.FromRgb(230, 70, 70)
                    : Color.FromRgb(40, 120, 220));

            var outline = new Pen(Brushes.White, 2);
            context.DrawEllipse(fill, outline, _points[i], PointRadius, PointRadius);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);


        var position = e.GetPosition(this);
        var pointer = e.GetCurrentPoint(this);

        var hitIndex = HitTest(position);

        if (pointer.Properties.IsRightButtonPressed)
        {
            if (hitIndex >= 0 && _points.Count > 2)
            {
                _points.RemoveAt(hitIndex);
                _selectedIndex = -1;
                _dragIndex = -1;
                InvalidateVisual();
            }

            e.Handled = true;
            return;
        }

        if (!pointer.Properties.IsLeftButtonPressed)
            return;

        if (e.ClickCount == 2 && hitIndex < 0)
        {
            AddPointAt(position);
            e.Handled = true;
            return;
        }

        if (hitIndex >= 0)
        {
            _selectedIndex = hitIndex;
            _dragIndex = hitIndex;
            e.Pointer.Capture(this);
            InvalidateVisual();
            e.Handled = true;
        }
        else
        {
            _selectedIndex = -1;
            InvalidateVisual();
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_dragIndex < 0 || _dragIndex >= _points.Count)
            return;

        var position = e.GetPosition(this);
        _points[_dragIndex] = ClampPoint(position);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_dragIndex >= 0)
        {
            _dragIndex = -1;
            e.Pointer.Capture(null);
            InvalidateVisual();
            e.Handled = true;
        }
    }

    private void AddPointAt(Point position)
    {
        position = ClampPoint(position);

        if (_points.Count < 2)
        {
            _points.Add(position);
            _selectedIndex = _points.Count - 1;
            InvalidateVisual();
            return;
        }

        var insertIndex = _points.Count;
        var bestDistance = double.MaxValue;

        for (var i = 0; i < _points.Count - 1; i++)
        {
            var distance = DistanceToSegment(position, _points[i], _points[i + 1]);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                insertIndex = i + 1;
            }
        }

        _points.Insert(insertIndex, position);
        _selectedIndex = insertIndex;
        InvalidateVisual();
    }

    private int HitTest(Point position)
    {
        for (var i = _points.Count - 1; i >= 0; i--)
        {
            var dx = position.X - _points[i].X;
            var dy = position.Y - _points[i].Y;

            if (dx * dx + dy * dy <= HitRadius * HitRadius)
                return i;
        }

        return -1;
    }

    private (Point C1, Point C2) GetBezierControls(int segmentIndex)
    {
        var p1 = _points[segmentIndex];
        var p2 = _points[segmentIndex + 1];

        var p0 = segmentIndex == 0
            ? p1
            : _points[segmentIndex - 1];

        var p3 = segmentIndex + 2 < _points.Count
            ? _points[segmentIndex + 2]
            : p2;

        var c1 = new Point(
            p1.X + (p2.X - p0.X) / 6.0,
            p1.Y + (p2.Y - p0.Y) / 6.0);

        var c2 = new Point(
            p2.X - (p3.X - p1.X) / 6.0,
            p2.Y - (p3.Y - p1.Y) / 6.0);

        return (c1, c2);
    }

    private Point ClampPoint(Point p)
    {
        var min = 14.0;
        var maxX = Math.Max(min, Bounds.Width - 14);
        var maxY = Math.Max(min, Bounds.Height - 14);

        return new Point(
            Math.Clamp(p.X, min, maxX),
            Math.Clamp(p.Y, min, maxY));
    }

    private void DrawGrid(DrawingContext context)
    {
        var pen = new Pen(
            new SolidColorBrush(Color.FromArgb(35, 0, 0, 0)),
            1);

        const double step = 40;

        for (var x = 0.0; x < Bounds.Width; x += step)
            context.DrawLine(pen, new Point(x, 0), new Point(x, Bounds.Height));


        for (var y = 0.0; y < Bounds.Height; y += step)
            context.DrawLine(pen, new Point(0, y), new Point(Bounds.Width, y));
    }

    private static double DistanceToSegment(Point p, Point a, Point b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;

        var lengthSquared = dx * dx + dy * dy;

        if (lengthSquared < 1e-9)
        {
            var ddx = p.X - a.X;
            var ddy = p.Y - a.Y;
            return Math.Sqrt(ddx * ddx + ddy * ddy);
        }

        var t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lengthSquared;
        t = Math.Clamp(t, 0, 1);

        var projX = a.X + t * dx;
        var projY = a.Y + t * dy;

        var ex = p.X - projX;
        var ey = p.Y - projY;

        return Math.Sqrt(ex * ex + ey * ey);
    }
}

public sealed class Part3BezierView : UserControl
{
    private readonly BezierEditor _editor = new();

    public Part3BezierView()
    {
        var addButton = new Button { Content = "Добавить точку" };
        addButton.Click += (_, _) => _editor.AddPoint();

        var deleteButton = new Button { Content = "Удалить выбранную" };
        deleteButton.Click += (_, _) => _editor.DeleteSelected();

        var resetButton = new Button { Content = "Сбросить" };
        resetButton.Click += (_, _) => _editor.ResetPoints();

        var hint = new TextBlock
        {
            Text = "Перетаскивайте опорные точки мышью. Двойной клик по пустому месту — добавить точку. ПКМ по точке — удалить.",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0)
        };

        var top = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(8)
        };

        top.Children.Add(addButton);
        top.Children.Add(deleteButton);
        top.Children.Add(resetButton);
        top.Children.Add(hint);

        var root = new DockPanel();
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);
        root.Children.Add(_editor);

        Content = root;
    }
}
