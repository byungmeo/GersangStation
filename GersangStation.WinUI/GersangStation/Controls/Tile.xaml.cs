// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace GersangStation.Controls;

public sealed partial class Tile : UserControl
{
    private bool _isPointerOver;
    private bool _isPressed;

    public enum TileVisualStyle
    {
        Default,
        Accent
    }

    public string Title
    {
        get { return (string)GetValue(TitleProperty); }
        set { SetValue(TitleProperty, value); }
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(Tile), new PropertyMetadata(null));

    public string Description
    {
        get { return (string)GetValue(DescriptionProperty); }
        set { SetValue(DescriptionProperty, value); }
    }

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(Tile), new PropertyMetadata(null));

    public object Source
    {
        get { return (object)GetValue(SourceProperty); }
        set { SetValue(SourceProperty, value); }
    }

    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.Register(nameof(Source), typeof(object), typeof(Tile), new PropertyMetadata(null));

    public string Link
    {
        get { return (string)GetValue(LinkProperty); }
        set { SetValue(LinkProperty, value); }
    }

    public static readonly DependencyProperty LinkProperty =
        DependencyProperty.Register(nameof(Link), typeof(string), typeof(Tile), new PropertyMetadata(null));

    public TileVisualStyle TileStyle
    {
        get { return (TileVisualStyle)GetValue(TileStyleProperty); }
        set { SetValue(TileStyleProperty, value); }
    }

    public static readonly DependencyProperty TileStyleProperty =
        DependencyProperty.Register(
            nameof(TileStyle),
            typeof(TileVisualStyle),
            typeof(Tile),
            new PropertyMetadata(TileVisualStyle.Default, OnTileStyleChanged));

    public Tile()
    {
        this.InitializeComponent();
        UpdateTileStyleState(useTransitions: false);
    }

    private static void OnTileStyleChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not Tile tile)
            return;

        tile.UpdateTileStyleState(useTransitions: true);
    }

    private void UpdateTileStyleState(bool useTransitions)
    {
        string stateName = GetTileStateName();
        VisualStateManager.GoToState(this, stateName, useTransitions);
    }

    private string GetTileStateName()
    {
        if (TileStyle != TileVisualStyle.Accent)
            return "DefaultNormal";

        if (_isPressed)
            return "AccentPressed";

        return _isPointerOver
            ? "AccentPointerOver"
            : "AccentNormal";
    }

    private void HyperlinkButton_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _isPointerOver = true;
        UpdateTileStyleState(useTransitions: true);
    }

    private void HyperlinkButton_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        _isPointerOver = false;
        _isPressed = false;
        UpdateTileStyleState(useTransitions: true);
    }

    private void HyperlinkButton_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isPressed = true;
        UpdateTileStyleState(useTransitions: true);
    }

    private void HyperlinkButton_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _isPressed = false;
        UpdateTileStyleState(useTransitions: true);
    }

    private void HyperlinkButton_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        _isPressed = false;
        UpdateTileStyleState(useTransitions: true);
    }
}
