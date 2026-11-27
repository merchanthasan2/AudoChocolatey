using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace QuantumUpdater.Views.Controls;

/// <summary>One item of a <see cref="VirtualList"/>: a group header or an app row.</summary>
public abstract record ListItem(string Id);

/// <summary>A group header. Count is the number of apps in the group. Expanded is the state shown.</summary>
public sealed record GroupItem(string Id, string Title, int Count, bool Expanded) : ListItem(Id);

/// <summary>An app row. The Id is the app key.</summary>
public sealed record RowItem(AppRowModel Model) : ListItem(Model.Key);

/// <summary>
/// Virtual list for the app screens: an ItemsRepeater in a ScrollViewer.
/// Group headers and app rows come from two recycle pools. An AppRow is reused, never created per item.
/// Pattern copied from tools/spikes/VirtualListSpike/MainWindow.cs (element factory, ElementPrepared).
/// The list does not filter or sort. The caller passes the visible items; collapsed groups have no rows.
/// WinUI control. Use it on the UI thread only.
/// </summary>
public sealed class VirtualList : Grid
{
    private readonly ItemsRepeater _repeater = new();
    private readonly ScrollViewer _scroll = new();
    private ObservableCollection<ListItem> _items = new();

    // The current items. ElementPrepared reads this list, so UpdateRow can change a row
    // without a collection replace (a replace makes the repeater rebuild the row, which flickers
    // during progress updates). The item kind at an index never changes, so the factory stays right.
    private List<ListItem> _data = new();
    private Dictionary<string, int> _rowIndex = new(StringComparer.Ordinal);
    private int _createdRows;

    /// <summary>Raised with the row key and the action, from every row.</summary>
    public event Action<string, RowAction>? RowAction;

    /// <summary>Raised with the group Id when a group header is clicked.</summary>
    public event Action<string>? GroupToggled;

    /// <summary>Raised whenever an <see cref="AppRow"/> is bound or updated, allowing async icon binding.</summary>
    public event Action<AppRow, AppRowModel>? RowPrepared;

    /// <summary>How many AppRow instances were created. Used by the gallery check.</summary>
    internal int CreatedRowCount => _createdRows;

    public VirtualList()
    {
        _repeater.ItemTemplate = new ItemFactory(this);
        _repeater.Layout = new StackLayout();
        _repeater.ItemsSource = _items;

        // Set each recycled element to the item at its index, as the spike does.
        _repeater.ElementPrepared += (_, e) => OnElementPrepared(e.Element, e.Index);

        _scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _scroll.Content = _repeater;
        Children.Add(_scroll);
    }

    /// <summary>Replaces all items. Rows and headers are reused from the pools.</summary>
    public void SetItems(IReadOnlyList<ListItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _data = new List<ListItem>(items);
        _rowIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < _data.Count; i++)
        {
            if (_data[i] is RowItem row)
            {
                _rowIndex[row.Model.Key] = i;
            }
        }

        _items = new ObservableCollection<ListItem>(_data);
        _repeater.ItemsSource = _items;
    }

    /// <summary>
    /// Replaces the row item with the same key. A realized row updates in place; the scroll position stays.
    /// Does nothing when no row has that key.
    /// </summary>
    public void UpdateRow(AppRowModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!_rowIndex.TryGetValue(model.Key, out var index))
        {
            return;
        }

        _data[index] = new RowItem(model);

        // Update a visible row in place. A row that is not visible gets the new model when it is prepared.
        if (_repeater.TryGetElement(index) is AppRow appRow)
        {
            appRow.Apply(model);
            RowPrepared?.Invoke(appRow, model);
        }
    }

    private void OnElementPrepared(UIElement element, int index)
    {
        if (index < 0 || index >= _data.Count)
        {
            return;
        }

        switch (_data[index])
        {
            case GroupItem group when element is GroupHeader header:
                header.Apply(group);
                break;

            case RowItem row when element is AppRow appRow:
                appRow.Apply(row.Model);

                // The top line is hidden on the first item and on the first row after a header.
                appRow.ShowTopLine = index > 0 && _data[index - 1] is RowItem;
                RowPrepared?.Invoke(appRow, row.Model);
                break;
        }
    }

    private AppRow CreateRow()
    {
        _createdRows++;
        var row = new AppRow();

        // Wired once, when the row is created. Recycling does not add a second handler.
        row.Action += OnRowAction;
        return row;
    }

    private GroupHeader CreateHeader()
    {
        var header = new GroupHeader();
        header.Click += (_, _) => GroupToggled?.Invoke(header.GroupId);
        return header;
    }

    private void OnRowAction(string key, RowAction action)
    {
        RowAction?.Invoke(key, action);
    }

    /// <summary>Builds rows and headers. Recycled elements wait in one pool for each kind.</summary>
    private sealed class ItemFactory : IElementFactory
    {
        private readonly VirtualList _owner;

        // Recycled elements wait here until the repeater asks again.
        private readonly Stack<UIElement> _rowPool = new();
        private readonly Stack<UIElement> _headerPool = new();

        public ItemFactory(VirtualList owner)
        {
            _owner = owner;
        }

        public UIElement GetElement(ElementFactoryGetArgs args)
        {
            if (args.Data is GroupItem)
            {
                return _headerPool.Count > 0 ? _headerPool.Pop() : _owner.CreateHeader();
            }

            return _rowPool.Count > 0 ? _rowPool.Pop() : _owner.CreateRow();
        }

        public void RecycleElement(ElementFactoryRecycleArgs args)
        {
            if (args.Element is GroupHeader)
            {
                _headerPool.Push(args.Element);
            }
            else
            {
                _rowPool.Push(args.Element);
            }
        }
    }

    /// <summary>Group header: the section header in a button. Click raises GroupToggled.</summary>
    private sealed class GroupHeader : Button
    {
        public GroupHeader()
        {
            // mockup: 01-updates.html .group (margin 20px 0 8px).
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            BorderThickness = new Thickness(0);
            Padding = new Thickness(0);
            HorizontalContentAlignment = HorizontalAlignment.Left;
            Margin = new Thickness(0, 20, 0, 8);
        }

        public string GroupId { get; private set; } = "";

        public void Apply(GroupItem group)
        {
            GroupId = group.Id;
            Content = Chrome.SectionHeader(group.Title, group.Count, group.Expanded);
            var state = group.Expanded ? "expanded" : "collapsed";
            AutomationProperties.SetName(this, $"{group.Title}, {group.Count} apps, {state}");
        }
    }
}
