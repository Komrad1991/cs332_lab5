using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using System.Drawing;
using System.Reflection;
using System.Reflection.Metadata;

namespace FractalApp.Parts;

public static class MidpointDisplacement
{
    public static List<List<Point>> Generate(
        int iterations,
        double roughness,
        double initialDisplacement,
        int seed)
    {
        var random = new Random(seed);
        var levels = new List<List<Point>>();

        var current = new List<Point>
        {
            new(0, 0),
            new(1, 0)
        };

        levels.Add(current);

        var amplitude = initialDisplacement;

        for (var level = 0; level < iterations; level++)
        {
            var next = new List<Point>(current.Count * 2 - 1);

            for (var i = 0; i < current.Count - 1; i++)
            {
                var a = current[i];
                var b = current[i + 1];

                var midX = (a.X + b.X) / 2;
                var midY = (a.Y + b.Y) / 2 +
                           (random.NextDouble() * 2 - 1) * amplitude;

                next.Add(a);
                next.Add(new Point(midX, midY));
            }

            next.Add(current[^1]);

            levels.Add(next);
            current = next;
            amplitude *= roughness;
        }

        return levels;
    }
}

public sealed class TerrainCanvas : Control
{
    public IReadOnlyList<List<Point>> Levels { get; set; } = Array.Empty<List<Point>>();
    public int CurrentStep { get; set; }
    public bool ShowAllSteps { get; set; } = true;

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        context.FillRectangle(
            Brushes.White,
            new Rect(0, 0, Bounds.Width, Bounds.Height));

        if (Levels.Count == 0 || Bounds.Width < 20 || Bounds.Height < 20)
            return;

        CurrentStep = Math.Clamp(CurrentStep, 0, Levels.Count - 1);

        var allPoints = Levels.SelectMany(x => x).ToList();
        var minY = allPoints.Min(p => p.Y);
        var maxY = allPoints.Max(p => p.Y);

        var spanY = maxY - minY;
        if (spanY < 0.5)
        {
            maxY += 0.5;
            minY -= 0.5;
            spanY = maxY - minY;
        }

        const double margin = 30;
        var scaleX = (Bounds.Width - 2 * margin) / 1.0;
        var scaleY = (Bounds.Height - 2 * margin) / spanY;
        var scale = Math.Min(scaleX, scaleY);

        var offsetX = (Bounds.Width - scale) / 2;
        var offsetY = (Bounds.Height - spanY * scale) / 2;

        Point Transform(Point p)
            => new(offsetX + p.X * scale, offsetY + (maxY - p.Y) * scale);

        var currentLevel = Levels[CurrentStep];

        var fillGeometry = new StreamGeometry();
        using (var fillContext = fillGeometry.Open())
        {
            fillContext.BeginFigure(Transform(currentLevel[0]), true);

            foreach (var point in currentLevel.Skip(1))
                fillContext.LineTo(Transform(point));

            fillContext.LineTo(new Point(
                Transform(currentLevel[^1]).X,
                Bounds.Height));

            fillContext.LineTo(new Point(
                Transform(currentLevel[0]).X,
                Bounds.Height));

            fillContext.EndFigure(true);
        }

        context.DrawGeometry(
            new SolidColorBrush(Color.FromArgb(160, 118, 168, 112)),
            null,
            fillGeometry);

        if (ShowAllSteps)
        {
            var previousPen = new Pen(
                new SolidColorBrush(Color.FromArgb(100, 90, 90, 90)),
                1);

            for (var step = 0; step < CurrentStep; step++)
                DrawLevel(context, Levels[step], Transform, previousPen);
        }

        var currentPen = new Pen(
            new SolidColorBrush(Color.FromRgb(35, 95, 45)),
            2.4);

        DrawLevel(context, currentLevel, Transform, currentPen);

        var endpointBrush = new SolidColorBrush(Color.FromRgb(35, 95, 45));
        foreach (var p in new[] { currentLevel[0], currentLevel[^1] })
        {
            var sp = Transform(p);
            context.DrawEllipse(endpointBrush, null, sp, 4, 4);
        }
    }

    private static void DrawLevel(
        DrawingContext context,
        IReadOnlyList<Point> level,
        Func<Point, Point> transform,
        Pen pen)
    {
        for (var i = 0; i < level.Count - 1; i++)
            context.DrawLine(pen, transform(level[i]), transform(level[i + 1]));
    }
}

public sealed class Part2TerrainView : UserControl
{
    private readonly TerrainCanvas _canvas = new();
    private readonly NumericUpDown _iterations = new()
    {
        Minimum = 0,
        Maximum = 12,
        Value = 8,
        Increment = 1,
        Width = 90
    };
    private readonly NumericUpDown _roughness = new()
    {
        Minimum = 0.25m,
        Maximum = 0.85m,
        Value = 0.55m,
        Increment = 0.01m,
        Width = 90
    };
    private readonly NumericUpDown _initialDisplacement = new()
    {
        Minimum = 0.1m,
        Maximum = 3.0m,
        Value = 1.0m,
        Increment = 0.1m,
        Width = 90
    };
    private readonly NumericUpDown _seed = new()
    {
        Minimum = 0,
        Maximum = 999999,
        Value = 42,
        Increment = 1,
        Width = 100
    };
    private readonly Slider _stepSlider = new()
    {
        Minimum = 0,
        Maximum = 8,
        Value = 8,
        TickFrequency = 1,
        IsSnapToTickEnabled = true,
        Width = 240
    };
    private readonly CheckBox _showAllSteps = new()
    {
        Content = "Показывать предыдущие шаги",
        IsChecked = true
    };
    private readonly TextBlock _stepLabel = new()
    {
        Text = "Шаг 8 из 8",
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(8, 0)
    };
    private readonly Button _playButton = new() { Content = "▶ Проиграть шаги" };
    private readonly DispatcherTimer _timer = new()
    {
        Interval = TimeSpan.FromMilliseconds(350)
    };

    public Part2TerrainView()
    {
        _timer.Tick += Timer_Tick;

        _stepSlider.PropertyChanged += (_, e) =>
        {
            if (e.Property != RangeBase.ValueProperty)
                return;

            _canvas.CurrentStep = (int)_stepSlider.Value;
            _stepLabel.Text = $"Шаг {_canvas.CurrentStep} из {Math.Max(0, _canvas.Levels.Count - 1)}";
            _canvas.InvalidateVisual();
        };

        _showAllSteps.IsCheckedChanged += (_, _) =>
        {
            _canvas.ShowAllSteps = _showAllSteps.IsChecked == true;
            _canvas.InvalidateVisual();
        };

        var generateButton = new Button { Content = "Построить" };
        generateButton.Click += (_, _) => GenerateTerrain();

        _playButton.Click += (_, _) => TogglePlay();

        var top = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(8)
        };

        top.Children.Add(new TextBlock
        {
            Text = "Итерации:",
            VerticalAlignment = VerticalAlignment.Center
        });
        top.Children.Add(_iterations);
        top.Children.Add(new TextBlock
        {
            Text = "Шероховатость:",
            VerticalAlignment = VerticalAlignment.Center
        });
        top.Children.Add(_roughness);
        top.Children.Add(new TextBlock
        {
            Text = "Нач. смещение:",
            VerticalAlignment = VerticalAlignment.Center
        });
        top.Children.Add(_initialDisplacement);
        top.Children.Add(new TextBlock
        {
            Text = "Seed:",
            VerticalAlignment = VerticalAlignment.Center
        });
        top.Children.Add(_seed);
        top.Children.Add(generateButton);
        top.Children.Add(_playButton);

        var bottom = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(8)
        };

        bottom.Children.Add(new TextBlock
        {
            Text = "Шаг:",
            VerticalAlignment = VerticalAlignment.Center
        });
        bottom.Children.Add(_stepSlider);
        bottom.Children.Add(_stepLabel);
        bottom.Children.Add(_showAllSteps);

        var root = new DockPanel();
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);

        var bottomBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
            Child = bottom
        };
        DockPanel.SetDock(bottomBorder, Dock.Bottom);
        root.Children.Add(bottomBorder);

        root.Children.Add(_canvas);
        Content = root;

        GenerateTerrain();
    }

    private void GenerateTerrain()
    {
        _timer.Stop();
        _playButton.Content = "▶ Проиграть шаги";

        var levels = MidpointDisplacement.Generate(
            iterations: (int)(_iterations.Value ?? 8),
            roughness: (double)(_roughness.Value ?? 0.55m),
            initialDisplacement: (double)(_initialDisplacement.Value ?? 1.0m),
            seed: (int)(_seed.Value ?? 42));

        _canvas.Levels = levels;
        _canvas.CurrentStep = levels.Count - 1;
        _canvas.ShowAllSteps = _showAllSteps.IsChecked == true;

        _stepSlider.Maximum = levels.Count - 1;
        _stepSlider.Value = _canvas.CurrentStep;
        _stepLabel.Text = $"Шаг {_canvas.CurrentStep} из {levels.Count - 1}";

        _canvas.InvalidateVisual();
    }

    private void TogglePlay()
    {
        if (_timer.IsEnabled)
        {
            _timer.Stop();
            _playButton.Content = "▶ Проиграть шаги";
            return;
        }

        if (_canvas.Levels.Count == 0)
            GenerateTerrain();

        _canvas.CurrentStep = 0;
        _stepSlider.Value = 0;
        _stepLabel.Text = $"Шаг 0 из {_canvas.Levels.Count - 1}";
        _canvas.InvalidateVisual();

        _playButton.Content = "⏸ Пауза";
        _timer.Start();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (_canvas.Levels.Count == 0)
        {
            _timer.Stop();
            return;
        }

        var maxStep = _canvas.Levels.Count - 1;

        if (_canvas.CurrentStep >= maxStep)
        {
            _timer.Stop();
            _playButton.Content = "▶ Проиграть шаги";
            return;
        }

        _canvas.CurrentStep++;
        _stepSlider.Value = _canvas.CurrentStep;
        _stepLabel.Text = $"Шаг {_canvas.CurrentStep} из {maxStep}";
        _canvas.InvalidateVisual();
    }
}