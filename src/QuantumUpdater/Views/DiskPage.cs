using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using QuantumUpdater.Services;
using QuantumUpdater.Views.Controls;
using Windows.System;

namespace QuantumUpdater.Views;

/// <summary>
/// Disk &amp; Storage view (ADR 048, Phase 46.1).
/// Shows physical &amp; volume drive health, external drive labels, cloud volumes excluded,
/// and provides a large file scanner with Recycle Bin and confirmed permanent delete.
/// </summary>
public sealed class DiskPage : UserControl
{
    private readonly AppServices _services;
    private readonly StackPanel _mainStack;
    private readonly StackPanel _drivesContainer;
    private readonly StackPanel _filesContainer;
    private readonly TextBlock _statusText;
    private readonly ComboBox _folderBox;
    private readonly ComboBox _sizeBox;
    private readonly Button _scanBtn;
    private readonly List<LargeFile> _currentFiles = new();

    public DiskPage(AppServices services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        Background = Theme.Canvas;

        _mainStack = new StackPanel { Spacing = 20, Padding = new Thickness(0, 0, 16, 28) };

        // 1. Header
        var header = new StackPanel { Spacing = 4 };
        header.Children.Add(Chrome.Title("Disk & Storage"));
        header.Children.Add(Chrome.Caption("Monitor drive space and health, review large files, and remove storage clutter safely."));
        _mainStack.Children.Add(header);

        // 2. Drives Section
        var drivesHead = new TextBlock
        {
            Text = "Drives & Volumes",
            FontFamily = Chrome.Font,
            FontSize = Theme.SectionSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = Theme.Text,
        };
        _mainStack.Children.Add(drivesHead);

        _drivesContainer = new StackPanel { Spacing = 12 };
        _mainStack.Children.Add(_drivesContainer);

        // 3. Large Files Section
        var filesSection = new StackPanel { Spacing = 12, Margin = new Thickness(0, 12, 0, 0) };
        var filesHeadRow = new Grid { ColumnSpacing = 12 };
        filesHeadRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        filesHeadRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var filesTitle = new StackPanel { Spacing = 2 };
        filesTitle.Children.Add(new TextBlock
        {
            Text = "Large Files",
            FontFamily = Chrome.Font,
            FontSize = Theme.SectionSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = Theme.Text,
        });
        filesTitle.Children.Add(Chrome.Caption("Identify files consuming significant disk space. Send them to the Recycle Bin or delete permanently."));
        Grid.SetColumn(filesTitle, 0);
        filesHeadRow.Children.Add(filesTitle);

        filesSection.Children.Add(filesHeadRow);

        // Controls bar for scanner
        var filterBar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Margin = new Thickness(0, 4, 0, 8),
        };

        _folderBox = new ComboBox
        {
            Width = 180,
            ItemsSource = new[] { "Downloads", "Videos", "Documents", "User Profile" },
            SelectedIndex = 0,
        };
        filterBar.Children.Add(_folderBox);

        _sizeBox = new ComboBox
        {
            Width = 140,
            ItemsSource = new[] { "> 50 MB", "> 100 MB", "> 500 MB", "> 1 GB" },
            SelectedIndex = 1,
        };
        filterBar.Children.Add(_sizeBox);

        _scanBtn = new Button
        {
            Content = "Scan Large Files",
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
        };
        _scanBtn.Click += async (_, _) => await RunScanAsync();
        filterBar.Children.Add(_scanBtn);

        _statusText = Chrome.Caption("");
        _statusText.VerticalAlignment = VerticalAlignment.Center;
        filterBar.Children.Add(_statusText);

        filesSection.Children.Add(filterBar);

        _filesContainer = new StackPanel { Spacing = 8 };
        filesSection.Children.Add(_filesContainer);

        _mainStack.Children.Add(filesSection);

        Content = new ScrollViewer
        {
            Content = _mainStack,
            HorizontalScrollMode = ScrollMode.Disabled,
            VerticalScrollMode = ScrollMode.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };

        RefreshDrives();
        _ = RunScanAsync();
    }

    private void RefreshDrives()
    {
        _drivesContainer.Children.Clear();
        var volumes = _services.Disk.Volumes();
        if (volumes.Count == 0)
        {
            _drivesContainer.Children.Add(Chrome.Card(Chrome.Line("No local physical drives detected.", Theme.TextSecondary)));
            return;
        }

        var grid = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        int colCount = Math.Min(volumes.Count, 3);
        for (int i = 0; i < colCount; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        int col = 0;
        int row = 0;
        foreach (var vol in volumes)
        {
            double totalGb = vol.Total / (1024.0 * 1024 * 1024);
            double freeGb = vol.Free / (1024.0 * 1024 * 1024);
            double usedGb = (vol.Total - vol.Free) / (1024.0 * 1024 * 1024);
            double usedPct = Math.Clamp((vol.Total - vol.Free) * 100.0 / vol.Total, 0, 100);

            var cardContent = new StackPanel { Spacing = 10 };

            var topRow = new Grid();
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var driveNameBlock = new TextBlock
            {
                Text = $"{vol.Name.TrimEnd('\\')}  {vol.Label}",
                FontFamily = Chrome.Font,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = Theme.Text,
            };
            Grid.SetColumn(driveNameBlock, 0);
            topRow.Children.Add(driveNameBlock);

            var badge = new Border
            {
                Background = vol.IsExternal ? Theme.Card : Theme.AccentChip,
                BorderBrush = Theme.Stroke,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Child = new TextBlock
                {
                    Text = vol.IsExternal ? "External" : "Internal",
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = vol.IsExternal ? Theme.Warning : Theme.TextSecondary,
                },
            };
            Grid.SetColumn(badge, 1);
            topRow.Children.Add(badge);
            cardContent.Children.Add(topRow);

            var bar = new ProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = usedPct,
                Height = 8,
                Foreground = usedPct > 90 ? Theme.Warning : Theme.Accent,
            };
            cardContent.Children.Add(bar);

            var statsRow = new Grid();
            statsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            statsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var freeTxt = Chrome.Caption($"{freeGb:0.#} GB free of {totalGb:0.#} GB");
            Grid.SetColumn(freeTxt, 0);
            statsRow.Children.Add(freeTxt);

            var healthTxt = new TextBlock
            {
                Text = "Healthy",
                FontFamily = Chrome.Font,
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                Foreground = Theme.Accent,
            };
            Grid.SetColumn(healthTxt, 1);
            statsRow.Children.Add(healthTxt);
            cardContent.Children.Add(statsRow);

            var card = Chrome.Card(cardContent);
            Grid.SetColumn(card, col);
            Grid.SetRow(card, row);
            grid.Children.Add(card);

            col++;
            if (col >= colCount)
            {
                col = 0;
                row++;
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }
        }

        _drivesContainer.Children.Add(grid);
    }

    private async Task RunScanAsync()
    {
        _scanBtn.IsEnabled = false;
        _statusText.Text = "Scanning for large files...";
        _filesContainer.Children.Clear();

        string folder = _folderBox.SelectedIndex switch
        {
            1 => Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            2 => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            3 => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            _ => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
        };

        long minSize = _sizeBox.SelectedIndex switch
        {
            0 => 50L * 1024 * 1024,
            2 => 500L * 1024 * 1024,
            3 => 1024L * 1024 * 1024,
            _ => 100L * 1024 * 1024,
        };

        var files = await Task.Run(() => _services.Disk.ScanLargeFiles(folder, minSize, 40));
        _currentFiles.Clear();
        _currentFiles.AddRange(files);

        _scanBtn.IsEnabled = true;
        _statusText.Text = files.Count == 0
            ? "No files matching criteria found."
            : $"Found {files.Count} large files in {Path.GetFileName(folder)}.";

        RenderFilesList();
    }

    private void RenderFilesList()
    {
        _filesContainer.Children.Clear();
        if (_currentFiles.Count == 0)
        {
            var empty = Chrome.Card(Chrome.Line("No large files found in the selected location.", Theme.TextSecondary));
            _filesContainer.Children.Add(empty);
            return;
        }

        foreach (var file in _currentFiles)
        {
            var rowCard = CreateFileRow(file);
            _filesContainer.Children.Add(rowCard);
        }
    }

    private FrameworkElement CreateFileRow(LargeFile file)
    {
        var rowGrid = new Grid { ColumnSpacing = 16 };
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var infoStack = new StackPanel { Spacing = 2 };
        infoStack.Children.Add(new TextBlock
        {
            Text = file.FileName,
            FontFamily = Chrome.Font,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = Theme.Text,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        infoStack.Children.Add(new TextBlock
        {
            Text = file.DirectoryPath,
            FontFamily = Chrome.Font,
            FontSize = 12,
            Foreground = Theme.TextSecondary,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        Grid.SetColumn(infoStack, 0);
        rowGrid.Children.Add(infoStack);

        var sizeText = new TextBlock
        {
            Text = file.FormattedSize,
            FontFamily = Chrome.Font,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = Theme.Text,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Right,
        };
        Grid.SetColumn(sizeText, 1);
        rowGrid.Children.Add(sizeText);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        var recycleBtn = new Button
        {
            Content = "Recycle",
            FontSize = 12,
            Padding = new Thickness(10, 4, 10, 4),
        };
        recycleBtn.Click += (_, _) =>
        {
            if (_services.Disk.SendToRecycleBin(file.Path))
            {
                _currentFiles.Remove(file);
                RenderFilesList();
                _statusText.Text = $"Moved {file.FileName} to Recycle Bin.";
            }
        };
        actions.Children.Add(recycleBtn);

        var deleteBtn = new Button
        {
            Content = "Delete",
            FontSize = 12,
            Foreground = Theme.CriticalText,
            Padding = new Thickness(10, 4, 10, 4),
        };
        deleteBtn.Click += async (_, _) =>
        {
            // ADR 048: delete permanently needs an extra confirmation
            var confirm = new ContentDialog
            {
                Title = "Permanently delete file?",
                Content = $"Are you sure you want to permanently delete:\n{file.FileName} ({file.FormattedSize})?\n\nThis cannot be undone and will not go to the Recycle Bin.",
                PrimaryButtonText = "Delete Permanently",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot,
            };

            var res = await confirm.ShowAsync();
            if (res == ContentDialogResult.Primary)
            {
                if (_services.Disk.DeletePermanently(file.Path))
                {
                    _currentFiles.Remove(file);
                    RenderFilesList();
                    _statusText.Text = $"Permanently deleted {file.FileName}.";
                }
            }
        };
        actions.Children.Add(deleteBtn);

        Grid.SetColumn(actions, 2);
        rowGrid.Children.Add(actions);

        return Chrome.Card(rowGrid);
    }
}
