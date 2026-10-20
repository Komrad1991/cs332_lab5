using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace FractalApp.Parts;

public sealed class LSystemDefinition
{
   public string Name { get; set; } = "L-system";
   public string Axiom { get; set; } = "F";
   public double AngleDegrees { get; set; } = 25;
   public double InitialDirectionDegrees { get; set; } = 90;
   public Dictionary<char, string> Rules { get; } = new();

   public LSystemDefinition Clone()
   {
      var copy = new LSystemDefinition
      {
         Name = Name,
         Axiom = Axiom,
         AngleDegrees = AngleDegrees,
         InitialDirectionDegrees = InitialDirectionDegrees
      };

      foreach (var rule in Rules)
         copy.Rules[rule.Key] = rule.Value;

      return copy;
   }
}

public static class LSystemParser
{
   public static LSystemDefinition ParseFile(string path)
      => Parse(File.ReadAllText(path));

   public static LSystemDefinition Parse(string text)
   {
      var lines = text
         .Replace("\r\n", "\n")
         .Split('\n')
         .Select(x => x.Trim())
         .Where(x => x.Length > 0 && !x.StartsWith("#"))
         .ToList();

      if (lines.Count == 0)
         throw new FormatException("Файл L-системы пуст.");

      var header = lines[0].Split(
         new[] { ' ', '\t' },
         StringSplitOptions.RemoveEmptyEntries);

      if (header.Length < 3)
         throw new FormatException(
            "Первая строка должна иметь вид: <атом> <угол> <начальное направление>");

      var definition = new LSystemDefinition
      {
         Axiom = header[0],
         AngleDegrees = double.Parse(header[1], CultureInfo.InvariantCulture),
         InitialDirectionDegrees =
            double.Parse(header[2], CultureInfo.InvariantCulture)
      };

      for (var i = 1; i < lines.Count; i++)
      {
         var line = lines[i];
         var eq = line.IndexOf('=');
         if (eq <= 0)
            continue;

         var predecessor = line[0];
         var replacement = line[(eq + 1)..].Trim().Replace(" ", "");
         definition.Rules[predecessor] = replacement;
      }

      return definition;
   }
}

public static class LSystemPresets
{
   public static List<LSystemDefinition> All { get; } = new()
   {
      new LSystemDefinition
      {
         Name = "Koch snowflake",
         Axiom = "F--F--F",
         AngleDegrees = 60,
         InitialDirectionDegrees = 0,
         Rules = { { 'F', "F+F--F+F" } }
      },
      new LSystemDefinition
      {
         Name = "Sierpinski triangle",
         Axiom = "F-G-G",
         AngleDegrees = 120,
         InitialDirectionDegrees = 0,
         Rules =
         {
            { 'F', "F-G+F+G-F" },
            { 'G', "GG" }
         }
      },
      new LSystemDefinition
      {
         Name = "Dragon curve",
         Axiom = "FX",
         AngleDegrees = 90,
         InitialDirectionDegrees = 0,
         Rules =
         {
            { 'X', "X+YF+" },
            { 'Y', "-FX-Y" }
         }
      },
      new LSystemDefinition
      {
         Name = "Plant / Fractal tree",
         Axiom = "X",
         AngleDegrees = 25,
         InitialDirectionDegrees = 90,
         Rules =
         {
            { 'X', "F+[[X]-X]-F[-FX]+X" },
            { 'F', "FF" }
         }
      },
      new LSystemDefinition
      {
         Name = "Hilbert curve",
         Axiom = "A",
         AngleDegrees = 90,
         InitialDirectionDegrees = 0,
         Rules =
         {
            { 'A', "+BF-AFA-FB+" },
            { 'B', "-AF+BFB+FA-" }
         }
      },
      new LSystemDefinition
      {
         Name = "Levy C curve",
         Axiom = "F",
         AngleDegrees = 45,
         InitialDirectionDegrees = 0,
         Rules = { { 'F', "+F--F+" } }
      }
   };
}

public static class LSystemEngine
{
   public static string Generate(LSystemDefinition definition, int iterations)
   {
      var current = definition.Axiom;

      for (var iteration = 0; iteration < iterations; iteration++)
      {
         var next = new System.Text.StringBuilder(
            Math.Min(current.Length * 2, 2_000_000));

         foreach (var symbol in current)
         {
            if (definition.Rules.TryGetValue(symbol, out var replacement))
               next.Append(replacement);
            else
               next.Append(symbol);

            if (next.Length > 2_000_000)
               return next.ToString(0, 2_000_000);
         }

         current = next.ToString();
      }

      return current;
   }
}

public readonly record struct TurtleSegment(Point Start, Point End, int Depth);

public static class LSystemTurtle
{
   private readonly record struct TurtleState(
      Point Position,
      double Angle,
      int Depth);

   public static List<TurtleSegment> Render(
      string word,
      LSystemDefinition definition,
      double stepLength,
      double randomAngleDegrees,
      double randomLengthPercent,
      int seed)
   {
      var random = new Random(seed);
      var result = new List<TurtleSegment>();
      var stack = new Stack<TurtleState>();

      var state = new TurtleState(
         new Point(0, 0),
         definition.InitialDirectionDegrees,
         0);

      foreach (var symbol in word)
      {
         switch (symbol)
         {
            case 'F':
            case 'G':
            {
               var length = stepLength *
                            (1 + (random.NextDouble() * 2 - 1) *
                               randomLengthPercent / 100.0);

               length = Math.Max(0.01, length);

               var radians = state.Angle * Math.PI / 180.0;
               var nextPoint = new Point(
                  state.Position.X + Math.Cos(radians) * length,
                  state.Position.Y - Math.Sin(radians) * length);

               result.Add(new TurtleSegment(state.Position, nextPoint,
                  state.Depth));
               state = state with { Position = nextPoint };
               break;
            }
            case 'f':
            {
               var length = Math.Max(0.01, stepLength);
               var radians = state.Angle * Math.PI / 180.0;
               var nextPoint = new Point(
                  state.Position.X + Math.Cos(radians) * length,
                  state.Position.Y - Math.Sin(radians) * length);

               state = state with { Position = nextPoint };
               break;
            }
            case '+':
               state = state with
               {
                  Angle = state.Angle + definition.AngleDegrees +
                          Jitter(random, randomAngleDegrees)
               };
               break;
            case '-':
               state = state with
               {
                  Angle = state.Angle - definition.AngleDegrees -
                          Jitter(random, randomAngleDegrees)
               };
               break;
            case '[':
               stack.Push(state);
               state = state with { Depth = state.Depth + 1 };
               break;
            case ']':
               if (stack.Count > 0)
                  state = stack.Pop();
               break;
            case '|':
               state = state with { Angle = state.Angle + 180 };
               break;
         }
      }

      return result;
   }

   private static double Jitter(Random random, double amplitude)
      => amplitude <= 0 ? 0 : (random.NextDouble() * 2 - 1) * amplitude;
}

public sealed class LSystemCanvas : Control
{
   public
      IReadOnlyList<TurtleSegment> Segments { get; set; } =
      Array.Empty<TurtleSegment>();

   public bool TreeMode { get; set; }
   public double BaseThickness { get; set; } = 8;
   public double TipThickness { get; set; } = 1.2;
   public Color BaseColor { get; set; } = Color.FromRgb(92, 64, 51);
   public Color TipColor { get; set; } = Color.FromRgb(50, 160, 60);

   public override void Render(DrawingContext context)
   {
      base.Render(context);

      context.FillRectangle(
         Brushes.White,
         new Rect(0, 0, Bounds.Width, Bounds.Height));

        if (Segments.Count == 0 || Bounds.Width < 4 || Bounds.Height < 4)
      return;

      var minX = Segments.Min(s => Math.Min(s.Start.X, s.End.X));
      var maxX = Segments.Max(s => Math.Max(s.Start.X, s.End.X));
      var minY = Segments.Min(s => Math.Min(s.Start.Y, s.End.Y));
      var maxY = Segments.Max(s => Math.Max(s.Start.Y, s.End.Y));

      var spanX = Math.Max(maxX - minX, 1e-6);
      var spanY = Math.Max(maxY - minY, 1e-6);

      const double margin = 24;
      var scale = Math.Min(
         (Bounds.Width - 2 * margin) / spanX,
         (Bounds.Height - 2 * margin) / spanY);

      var offsetX = (Bounds.Width - spanX * scale) / 2 - minX * scale;
      var offsetY = (Bounds.Height - spanY * scale) / 2 - minY * scale;


      Point Transform(Point p)
         => new(offsetX + p.X * scale, offsetY + p.Y * scale);


      if (!TreeMode)
      {
         var pen = new Pen(new SolidColorBrush(Color.FromRgb(20, 120, 85)),
            1.4);

         foreach (var segment in Segments)
            context.DrawLine(pen, Transform(segment.Start),
               Transform(segment.End));

         return;
      }

      var maxDepth = Segments.Max(s => s.Depth);
      var pens = new Dictionary<int, Pen>();

      foreach (var segment in Segments)
      {
         if (!pens.TryGetValue(segment.Depth, out var pen))
         {
            var t = maxDepth == 0 ? 0 : segment.Depth / (double)maxDepth;
            var thickness = Math.Max(
               0.35,
               BaseThickness + (TipThickness - BaseThickness) * t);

            var color = LerpColor(BaseColor, TipColor, t);
            pen = new Pen(new SolidColorBrush(color), thickness);
            pens[segment.Depth] = pen;
         }

         context.DrawLine(pen, Transform(segment.Start),
            Transform(segment.End));
      }
   }

   private static Color LerpColor(Color a, Color b, double t)
   {
      t = Math.Clamp(t, 0, 1);

      return Color.FromArgb(
         (byte)(a.A + (b.A - a.A) * t),
         (byte)(a.R + (b.R - a.R) * t),
         (byte)(a.G + (b.G - a.G) * t),
         (byte)(a.B + (b.B - a.B) * t));
   }
}

public sealed class Part1View : UserControl
{
   private readonly LSystemCanvas _canvas = new();
   private readonly ComboBox _presetBox = new() { Width = 210 };

   private readonly NumericUpDown _iterations = new()
   {
      Minimum = 0,
      Maximum = 7,
      Value = 4,
      Increment = 1,
      Width = 180
   };

   private readonly NumericUpDown _randomAngle = new()
   {
      Minimum = 0,
      Maximum = 45,
      Value = 0,
      Increment = 1,
      Width = 180
   };

   private readonly NumericUpDown _randomLength = new()
   {
      Minimum = 0,
      Maximum = 50,
      Value = 0,
      Increment = 1,
      Width = 180
   };

   private readonly TextBlock _info = new() { Margin = new Thickness(8, 4) };

   private LSystemDefinition _definition = LSystemPresets.All[0].Clone();
   private bool _suppressPreset;

   public Part1View()
   {
      _presetBox.ItemsSource = LSystemPresets.All.Select(p => p.Name).ToList();
      _presetBox.SelectedIndex = 0;

      _presetBox.SelectionChanged += (_, _) =>
      {
         if (_suppressPreset || _presetBox.SelectedIndex < 0)
            return;

         _definition = LSystemPresets.All[_presetBox.SelectedIndex].Clone();
         Rebuild();
      };
      var buildButton = new Button { Content = "Построить" };
      buildButton.Click += (_, _) => Rebuild();

      var loadButton = new Button { Content = "Загрузить файл..." };
      loadButton.Click += LoadFileButton_Click;

      var top = new StackPanel
      {
         Orientation = Orientation.Horizontal,
         Spacing = 8,
         Margin = new Thickness(8)
      };

      top.Children.Add(new TextBlock
      {
         Text = "Пресет:",
         VerticalAlignment = VerticalAlignment.Center
      });
      top.Children.Add(_presetBox);
      top.Children.Add(new TextBlock
      {
         Text = "Итерации:",
         VerticalAlignment = VerticalAlignment.Center
      });
      top.Children.Add(_iterations);
      top.Children.Add(new TextBlock
      {
         Text = "Случ. угол:",
         VerticalAlignment = VerticalAlignment.Center
      });
      top.Children.Add(_randomAngle);
      top.Children.Add(new TextBlock
      {
         Text = "Случ. длина, %:",
         VerticalAlignment = VerticalAlignment.Center
      });
      top.Children.Add(_randomLength);
      top.Children.Add(buildButton);
      top.Children.Add(loadButton);

      var root = new DockPanel();
      DockPanel.SetDock(top, Dock.Top);
      root.Children.Add(top);

      var infoBorder = new Border
      {
         Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
         Child = _info
      };
      DockPanel.SetDock(infoBorder, Dock.Bottom);
      root.Children.Add(infoBorder);

      root.Children.Add(_canvas);
      Content = root;

      Rebuild();
   }

   private void Rebuild()
   {
      try
      {
         var iterations = (int)(_iterations.Value ?? 4);
         var word = LSystemEngine.Generate(_definition, iterations);

         var segments = LSystemTurtle.Render(
            word,
            _definition,
            1.0,
            (double)(_randomAngle.Value ?? 0),
            (double)(_randomLength.Value ?? 0),
            seed: 12345);

         _canvas.TreeMode = false;
         _canvas.Segments = segments;
         _canvas.InvalidateVisual();

         _info.Text =
            $"Символов: {word.Length:N0}, отрезков: {segments.Count:N0}";
      }
      catch (Exception ex)
      {
         _info.Text = "Ошибка: " + ex.Message;
      }
   }

   private async void LoadFileButton_Click(object? sender,
      Avalonia.Interactivity.RoutedEventArgs e)
   {
      var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
      if (storage is null)
         return;

      var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
      {
         Title = "Выберите файл L-системы",
         AllowMultiple = false,
         FileTypeFilter = new[]
         {
            new FilePickerFileType("Текстовые файлы")
            {
               Patterns = new[] { "*.txt", "*.lsys" }
            }
         }
      });

      if (files.Count == 0)
         return;

      try
      {
         using var stream = await files[0].OpenReadAsync();
         using var reader = new StreamReader(stream);
         var text = await reader.ReadToEndAsync();

         _definition = LSystemParser.Parse(text);

         _suppressPreset = true;
         _presetBox.SelectedIndex = -1;
         _suppressPreset = false;

         _info.Text = "Загружено: " + files[0].Name;
         Rebuild();
      }
      catch (Exception ex)
      {
         _info.Text = "Ошибка загрузки: " + ex.Message;
      }
   }
}

public sealed class Part1TreeView : UserControl
{
   private readonly LSystemCanvas _canvas = new()
   {
      TreeMode = true,
      BaseThickness = 10,
      TipThickness = 1.2,
      BaseColor = Color.FromRgb(92, 64, 51),
      TipColor = Color.FromRgb(50, 170, 60)
   };

   private readonly ComboBox _presetBox = new() { Width = 210 };

   private readonly NumericUpDown _iterations = new()
   {
      Minimum = 1,
      Maximum = 6,
      Value = 5,
      Increment = 1,
      Width = 180
   };

   private readonly NumericUpDown _randomAngle = new()
   {
      Minimum = 0,
      Maximum = 35,
      Value = 10,
      Increment = 1,
      Width = 180
   };

   private readonly NumericUpDown _randomLength = new()
   {
      Minimum = 0,
      Maximum = 30,
      Value = 6,
      Increment = 1,
      Width = 180
   };

   private readonly NumericUpDown _baseThickness = new()
   {
      Minimum = 1,
      Maximum = 25,
      Value = 10,
      Increment = 0.5m,
      Width = 180
   };

   private readonly NumericUpDown _tipThickness = new()
   {
      Minimum = 0.3m,
      Maximum = 8,
      Value = 1.2m,
      Increment = 0.1m,
      Width = 180
   };

   private readonly NumericUpDown _seed = new()
   {
      Minimum = 0,
      Maximum = 999999,
      Value = 42,
      Increment = 1,
      Width = 200
   };

   private readonly TextBlock _info = new() { Margin = new Thickness(8, 4) };

   private LSystemDefinition _definition =
      LSystemPresets.All.First(p => p.Name.StartsWith("Plant")).Clone();

   public Part1TreeView()
   {
      _presetBox.ItemsSource = LSystemPresets.All.Select(p => p.Name).ToList();
      _presetBox.SelectedIndex =
         LSystemPresets.All.FindIndex(p => p.Name.StartsWith("Plant"));

      _presetBox.SelectionChanged += (_, _) =>
      {
         if (_presetBox.SelectedIndex < 0)
            return;

         _definition = LSystemPresets.All[_presetBox.SelectedIndex].Clone();
         Rebuild();
      };

      var buildButton = new Button { Content = "Построить дерево" };
      buildButton.Click += (_, _) => Rebuild();

      var top = new StackPanel
      {
         Orientation = Orientation.Horizontal,
         Spacing = 8,
         Margin = new Thickness(8)
      };

      top.Children.Add(new TextBlock
      {
         Text = "Пресет:",
         VerticalAlignment = VerticalAlignment.Center
      });
      top.Children.Add(_presetBox);
      top.Children.Add(new TextBlock
      {
         Text = "Итерации:",
         VerticalAlignment = VerticalAlignment.Center
      });
      top.Children.Add(_iterations);
      top.Children.Add(new TextBlock
      {
         Text = "Случ. угол:",
         VerticalAlignment = VerticalAlignment.Center
      });
      top.Children.Add(_randomAngle);
      top.Children.Add(new TextBlock
      {
         Text = "Случ. длина, %:",
         VerticalAlignment = VerticalAlignment.Center
      });
      top.Children.Add(_randomLength);
      top.Children.Add(new TextBlock
      {
         Text = "Толщина базы:",
         VerticalAlignment = VerticalAlignment.Center
      });
      top.Children.Add(_baseThickness);
      top.Children.Add(new TextBlock
      {
         Text = "Толщина кончика:",
         VerticalAlignment = VerticalAlignment.Center
      });
      top.Children.Add(_tipThickness);
      top.Children.Add(new TextBlock
      {
         Text = "Seed:",
         VerticalAlignment = VerticalAlignment.Center
      });
      top.Children.Add(_seed);
      top.Children.Add(buildButton);

      var root = new DockPanel();
      DockPanel.SetDock(top, Dock.Top);
      root.Children.Add(top);

      var infoBorder = new Border
      {
         Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
         Child = _info
      };
      DockPanel.SetDock(infoBorder, Dock.Bottom);
      root.Children.Add(infoBorder);

      root.Children.Add(_canvas);
      Content = root;

      Rebuild();
   }

   private void Rebuild()
   {
      try
      {
         var iterations = (int)(_iterations.Value ?? 5);

         _canvas.BaseThickness = (double)(_baseThickness.Value ?? 10);
         _canvas.TipThickness = (double)(_tipThickness.Value ?? 1.2m);

         var word = LSystemEngine.Generate(_definition, iterations);

         var segments = LSystemTurtle.Render(
            word,
            _definition,
            stepLength: 1.0,
            randomAngleDegrees: (double)(_randomAngle.Value ?? 10),
            randomLengthPercent: (double)(_randomLength.Value ?? 6),
            seed: (int)(_seed.Value ?? 42));

         _canvas.TreeMode = true;
         _canvas.Segments = segments;
         _canvas.InvalidateVisual();

         _info.Text =
            $"Символов: {word.Length:N0}, ветвей: {segments.Count:N0}. " +
            "Цвет плавно меняется от коричневого к зелёному, толщина уменьшается к кончикам.";
      }
      catch (Exception ex)
      {
         _info.Text = "Ошибка: " + ex.Message;
      }
   }
}
