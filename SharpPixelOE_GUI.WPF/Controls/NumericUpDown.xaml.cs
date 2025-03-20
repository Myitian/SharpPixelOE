using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Myitian.Controls;

public partial class NumericUpDown : UserControl, INotifyPropertyChanged
{
    static readonly MethodInfo? ButtonBaseIsPressedSetter = typeof(ButtonBase).GetProperty("IsPressed", BindingFlags.Instance | BindingFlags.NonPublic)?.SetMethod;

    public class NumericUpDownEventArgs : EventArgs
    {
        public decimal OldValue { get; init; }
        public decimal NewValue { get; init; }
    }
    private bool suppressTextChange = false;
    private bool byCode = false;

    public event Action<object, NumericUpDownEventArgs>? OnValueChanged;

    public NumericUpDown()
    {
        InitializeComponent();
    }

    private void SetText(decimal value)
    {
        byCode = true;
        NUDTextBox.Text = value.ToString(TextFormat);
        byCode = false;
    }

    private static int ClampToInt(decimal value)
    {
        if (value < int.MinValue)
            return int.MinValue;
        else if (value > int.MaxValue)
            return int.MaxValue;
        else
            return (int)Math.Round(value);
    }

    /// <summary>按钮字体大小</summary>
    public static readonly DependencyProperty ButtonFontSizeProperty =
        DependencyProperty.Register(nameof(ButtonFontSize), typeof(double), typeof(NumericUpDown),
            new(8.0, static (d, e) =>
            {
                if (d is not NumericUpDown self)
                    return;
                self.OnPropertyChanged(nameof(ButtonFontSize));
            }));
    [Description("Button FontSize")]
    [Category("Text")]
    public double ButtonFontSize
    {
        get => (double)GetValue(ButtonFontSizeProperty);
        set => SetValue(ButtonFontSizeProperty, value);
    }

    /// <summary>文字对齐</summary>
    public static readonly DependencyProperty TextAlignmentProperty =
        DependencyProperty.Register(nameof(TextAlignment), typeof(TextAlignment), typeof(NumericUpDown),
            new(TextAlignment.Right, static (d, e) =>
            {
                if (d is not NumericUpDown self)
                    return;
                self.OnPropertyChanged(nameof(TextAlignment));
            }));
    [Description("Text Alignment")]
    [Category("Text")]
    public TextAlignment TextAlignment
    {
        get => (TextAlignment)GetValue(TextAlignmentProperty);
        set => SetValue(TextAlignmentProperty, value);
    }

    public static object ClampCoerceValueCallback(DependencyObject d, object baseValue)
    {
        if (d is not NumericUpDown self)
            return baseValue;
        decimal value = (decimal)baseValue;
        if (self.IntegerMode)
            value = ClampToInt(value);
        return value;
    }

    /// <summary>整数模式</summary>
    public static readonly DependencyProperty IntegerModeProperty =
        DependencyProperty.Register(nameof(IntegerMode), typeof(bool), typeof(NumericUpDown),
            new(false, static (o, s) =>
            {
                if (o is not NumericUpDown self)
                    return;
                self.OnPropertyChanged(nameof(IntegerMode));
                if (s.NewValue is true)
                {
                    self.MinValue = ClampToInt(self.MinValue);
                    self.MaxValue = ClampToInt(self.MaxValue);
                    self.Value = ClampToInt(self.Value);
                    self.Step = ClampToInt(self.Step);
                }
            }));
    [Description("Integer Mode")]
    [Category("Common Properties")]
    public bool IntegerMode
    {
        get => (bool)GetValue(IntegerModeProperty);
        set => SetValue(IntegerModeProperty, value);
    }

    /// <summary>最小值</summary>
    public static readonly DependencyProperty MinValueProperty =
        DependencyProperty.Register(nameof(MinValue), typeof(decimal), typeof(NumericUpDown),
            new(0m, static (d, e) =>
            {
                if (d is not NumericUpDown self)
                    return;
                self.OnPropertyChanged(nameof(MinValue));
            },
            ClampCoerceValueCallback));
    [Description("Min Value")]
    [Category("Common Properties")]
    public decimal MinValue
    {
        get => (decimal)GetValue(MinValueProperty);
        set
        {
            if (IntegerMode)
                value = ClampToInt(value);
            SetValue(MinValueProperty, value);
        }
    }

    /// <summary>最大值</summary>
    public static readonly DependencyProperty MaxValueProperty =
        DependencyProperty.Register(nameof(MaxValue), typeof(decimal), typeof(NumericUpDown),
            new(100m, static (d, e) =>
            {
                if (d is not NumericUpDown self)
                    return;
                self.OnPropertyChanged(nameof(MaxValue));
            },
            ClampCoerceValueCallback));
    [Description("Max Value")]
    [Category("Common Properties")]
    public decimal MaxValue
    {
        get => (decimal)GetValue(MaxValueProperty);
        set
        {
            if (IntegerMode)
                value = ClampToInt(value);
            SetValue(MaxValueProperty, value);
        }
    }

    /// <summary>步长</summary>
    public static readonly DependencyProperty StepProperty =
        DependencyProperty.Register(nameof(Step), typeof(decimal), typeof(NumericUpDown),
            new(1m, static (d, e) =>
            {
                if (d is not NumericUpDown self)
                    return;
                self.OnPropertyChanged(nameof(Step));
            },
            ClampCoerceValueCallback));
    [Description("Step")]
    [Category("Common Properties")]
    public decimal Step
    {
        get => (decimal)GetValue(StepProperty);
        set
        {
            if (IntegerMode)
                value = ClampToInt(value);
            SetValue(StepProperty, value);
        }
    }

    /// <summary>值</summary>
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(decimal), typeof(NumericUpDown),
            new FrameworkPropertyMetadata(0m, static (d, e) =>
            {
                if (d is not NumericUpDown self)
                    return;
                decimal value = (decimal)e.NewValue;
                decimal oldV = (decimal)e.OldValue;
                self.OnPropertyChanged(nameof(Value));
                self.OnValueChanged?.Invoke(self, new() { OldValue = oldV, NewValue = value });
                if (!self.suppressTextChange)
                    self.SetText(self.Value);
            }, (d, baseValue) =>
            {
                if (d is not NumericUpDown self)
                    return baseValue;
                decimal value = (decimal)baseValue;
                Math.Clamp(value, self.MinValue, self.MaxValue);
                if (self.IntegerMode)
                    value = ClampToInt(value);
                return value;
            })
            {
                BindsTwoWayByDefault = true
            });
    [Description("Value")]
    [Category("Common Properties")]
    public decimal Value
    {
        get => (decimal)GetValue(ValueProperty);
        set
        {
            Math.Clamp(value, MinValue, MaxValue);
            if (IntegerMode)
                value = ClampToInt(value);
            SetValue(ValueProperty, value);
        }
    }

    /// <summary>文本格式</summary>
    public static readonly DependencyProperty TextFormatProperty =
        DependencyProperty.Register(nameof(TextFormat), typeof(string), typeof(NumericUpDown),
            new UIPropertyMetadata(null,
                (d, e) =>
                {
                    if (d is not NumericUpDown self)
                        return;
                    self.SetText(self.Value);
                }));
    [Description("Text Format")]
    [Category("Common Properties")]
    public string? TextFormat
    {
        get => GetValue(TextFormatProperty) as string;
        set => SetValue(ValueProperty, value);
    }

    /// <summary>
    /// 属性已改变
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;
    /// <summary>
    /// 属性已改变
    /// </summary>
    protected void OnPropertyChanged(string? name = null) => PropertyChanged?.Invoke(this, new(name));

    private void NUDButtonUP_Click(object sender, RoutedEventArgs e)
    {
        Value = Math.Clamp(Value + Step, MinValue, MaxValue);
    }

    private void NUDButtonDown_Click(object sender, RoutedEventArgs e)
    {
        Value = Math.Clamp(Value - Step, MinValue, MaxValue);
    }

    private void NUDTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Up:
                NUDButtonUP.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                ButtonBaseIsPressedSetter?.Invoke(NUDButtonUP, [true]);
                break;
            case Key.Down:
                NUDButtonDown.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                ButtonBaseIsPressedSetter?.Invoke(NUDButtonDown, [true]);
                break;
            case Key.Enter:
                suppressTextChange = false;
                SetText(Value);
                break;
        }
    }

    private void NUDTextBox_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Up:
                ButtonBaseIsPressedSetter?.Invoke(NUDButtonUP, [false]);
                break;
            case Key.Down:
                ButtonBaseIsPressedSetter?.Invoke(NUDButtonDown, [false]);
                break;
        }
    }

    private void NUDTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (byCode)
            return;
        suppressTextChange = true;
        string text = NUDTextBox.Text;
        if (decimal.TryParse(text, out decimal n))
        {
            if (n > MaxValue)
            {
                Value = MaxValue;
            }
            else if (n < MinValue)
            {
                Value = MinValue;
            }
            else
            {
                Value = n;
            }
        }
    }

    private void NUDTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        suppressTextChange = false;
        SetText(Value);
    }

    private void NUDTextBox_Loaded(object sender, RoutedEventArgs e)
    {
        SetText(Value);
    }
}
