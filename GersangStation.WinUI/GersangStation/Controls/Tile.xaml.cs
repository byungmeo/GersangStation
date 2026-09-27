using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GersangStation.Controls;

public sealed partial class Tile : UserControl
{
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(Tile), new PropertyMetadata(null));

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(Tile), new PropertyMetadata(null));

    public object Source
    {
        get => (object)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.Register(nameof(Source), typeof(object), typeof(Tile), new PropertyMetadata(null));

    public string Link
    {
        get => (string)GetValue(LinkProperty);
        set => SetValue(LinkProperty, value);
    }

    public static readonly DependencyProperty LinkProperty =
        DependencyProperty.Register(nameof(Link), typeof(string), typeof(Tile), new PropertyMetadata(null));

    public Tile()
    {
        InitializeComponent();
    }
}
