using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace AstroRig.NinaPlugin;

public partial class RigPanel : UserControl
{
    private static readonly string InventoryDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ImagingRig");
    private static readonly string LastPluginInventory = Path.Combine(InventoryDirectory, "nina-last-inventory.txt");
    private string path = Path.Combine(InventoryDirectory, "inventory.json");
    private Inventory? inventory;

    public RigPanel()
    {
        InitializeComponent();
        try
        {
            var lastPath = File.Exists(LastPluginInventory) ? LastPluginInventory : Path.Combine(InventoryDirectory, "last-inventory.txt");
            if (File.Exists(lastPath))
            {
                var recent = File.ReadAllText(lastPath).Trim();
                if (File.Exists(recent)) path = recent;
            }
            LoadInventory(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Status.Text = error.Message;
        }
    }

    private void OpenInventory(object sender, RoutedEventArgs args)
    {
        var dialog = new OpenFileDialog { Filter = "AstroRig inventory (*.json)|*.json", CheckFileExists = true };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true) OpenAndRemember(dialog.FileName);
    }

    private void ReloadInventory(object sender, RoutedEventArgs args) => LoadInventory(path);

    private void OpenAndRemember(string file)
    {
        if (!LoadInventory(file)) return;
        try
        {
            Directory.CreateDirectory(InventoryDirectory);
            File.WriteAllText(LastPluginInventory, file);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Status.Text = $"Inventory opened. Could not remember its path. {error.Message}";
        }
    }

    private bool LoadInventory(string file)
    {
        try
        {
            if (!File.Exists(file))
            {
                Status.Text = "Create a new inventory or open an existing AstroRig inventory.";
                return false;
            }
            var loaded = Inventory.Load(file);
            var selected = (Rigs.SelectedItem as Rig)?.Name;
            inventory = loaded;
            path = file;
            Rigs.ItemsSource = loaded.Rigs;
            Rigs.SelectedItem = loaded.Rigs.Find(rig => rig.Name == selected) ?? loaded.Rigs.FirstOrDefault();
            ExportButton.IsEnabled = true;
            Status.Text = file;
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            Status.Text = $"Could not open inventory. {error.Message}";
            return false;
        }
    }

    private void ShowRig(object sender, SelectionChangedEventArgs args)
    {
        if (inventory == null || Rigs.SelectedItem is not Rig rig)
        {
            Components.ItemsSource = null;
            Summary.Text = "Select a rig.";
            return;
        }

        Components.ItemsSource = inventory.Resolve(rig).Select(pair => new ComponentRow(
            pair.Item, pair.Assignment)).ToArray();
        var metrics = RigMetrics.Calculate(inventory, rig);
        Summary.Text = $"Focal length {Number(metrics.FocalLength, "mm")}   ·   f/{Number(metrics.FocalRatio, "")}\n" +
            $"Image scale {Number(metrics.ImageScale, "arcsec/px")}   ·   Field of view {Number(metrics.WidthDegrees, "°")} × {Number(metrics.HeightDegrees, "°")}\n" +
            $"Payload {Number(metrics.Payload, "kg")}   ·   Rated {Number(metrics.RatedPayload, "kg")}   ·   Imaging target {Number(metrics.ImagingTarget, "kg")}   ·   Headroom {Number(metrics.Headroom, "kg")}\n" +
            $"Backfocus target {Number(metrics.RequiredBackfocus, "mm")}   ·   Recorded {Number(metrics.Spacing, "mm")}   ·   Remaining {Number(metrics.Remaining, "mm")}";
        if (metrics.MissingWeights > 0) Summary.Text += $"\nMissing weights: {metrics.MissingWeights}";
    }

    private static string Number(double? value, string unit) => value is double numeric ? $"{numeric:0.##} {unit}".TrimEnd() : "—";

    private void SelectComponent(object sender, SelectionChangedEventArgs args) =>
        SlotsButton.IsEnabled = Components.SelectedItem is ComponentRow { Item.Category: Role.FilterWheel };

    private void ExportInventory(object sender, RoutedEventArgs args)
    {
        if (inventory == null) return;
        var dialog = new SaveFileDialog { Filter = "Excel workbook (*.xlsx)|*.xlsx", FileName = "AstroRig Inventory.xlsx" };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        try
        {
            ExcelExport.Write(dialog.FileName, inventory);
            Status.Text = $"Exported {dialog.FileName}";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Status.Text = $"Could not export inventory. {error.Message}";
        }
    }

    private void EditSlots(object sender, RoutedEventArgs args)
    {
        if (inventory == null || Components.SelectedItem is not ComponentRow { Item: { Category: Role.FilterWheel } wheel }) return;
        if (wheel.FilterSlotCount is < 1 or > 64)
        {
            Status.Text = "This wheel needs a slot count. Add a Filter Wheel with its slot count or update the existing wheel in standalone AstroRig.";
            return;
        }
        var filters = inventory.Equipment.Where(item => item.Category == Role.Filter)
            .OrderBy(item => item.ToString(), StringComparer.CurrentCultureIgnoreCase).ToArray();
        if (filters.Length == 0)
        {
            Status.Text = "Use Add equipment → Filter to add the individual filters to this inventory.";
            return;
        }

        var options = new[] { new FilterChoice(null, "(Empty)") }.Concat(filters.Select(
            item => new FilterChoice(item.Id, item.ToString()))).ToArray();
        var rows = new StackPanel { Margin = new Thickness(12) };
        var pickers = new List<ComboBox>();
        for (var slot = 1; slot <= wheel.FilterSlotCount; slot++)
        {
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 5) };
            var picker = new ComboBox { ItemsSource = options, DisplayMemberPath = nameof(FilterChoice.Name) };
            var selected = wheel.FilterSlots?.GetValueOrDefault(slot);
            picker.SelectedItem = options.FirstOrDefault(option => option.Id == selected) ?? options[0];
            row.Children.Add(new Label { Content = $"Slot {slot}", Width = 80, Target = picker });
            row.Children.Add(picker);
            rows.Children.Add(row);
            pickers.Add(picker);
        }
        var dialog = new Window
        {
            Owner = Window.GetWindow(this), Title = $"Filter slots — {wheel}", Width = 490, Height = 400,
            MinWidth = 310, MinHeight = 220, WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false
        };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(12) };
        var save = new Button { Content = "Save slots", IsDefault = true, Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(0, 0, 8, 0) };
        save.Click += (_, _) => dialog.DialogResult = true;
        buttons.Children.Add(save);
        buttons.Children.Add(new Button { Content = "Cancel", IsCancel = true, Padding = new Thickness(10, 4, 10, 4) });
        var layout = new DockPanel();
        DockPanel.SetDock(buttons, Dock.Bottom);
        layout.Children.Add(buttons);
        layout.Children.Add(new ScrollViewer { Content = rows, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        dialog.Content = layout;
        if (dialog.ShowDialog() != true) return;

        var slots = new Dictionary<int, Guid>();
        for (var slot = 1; slot <= pickers.Count; slot++)
            if (pickers[slot - 1].SelectedItem is FilterChoice { Id: Guid id }) slots.Add(slot, id);

        try
        {
            if (!File.Exists(path)) throw new FileNotFoundException("The source inventory was moved or removed.");
            var latest = Inventory.Load(path);
            var current = latest.Equipment.Find(item => item.Id == wheel.Id && item.Category == Role.FilterWheel);
            if (current == null || current.FilterSlotCount != wheel.FilterSlotCount ||
                !SameSlots(current.FilterSlots, wheel.FilterSlots) ||
                slots.Values.Any(id => !latest.Equipment.Any(item => item.Id == id && item.Category == Role.Filter)))
            {
                Status.Text = "The wheel or its filters changed on disk. Reload the inventory and try again.";
                return;
            }
            current.FilterSlots = slots.Count == 0 ? null : slots;
            latest.Save(path);
            LoadInventory(path);
            Status.Text = $"Saved filter slots in {path}";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            Status.Text = $"Could not save filter slots. {error.Message}";
        }
    }

    private static bool SameSlots(Dictionary<int, Guid>? first, Dictionary<int, Guid>? second) =>
        (first?.Count ?? 0) == (second?.Count ?? 0) &&
        (first == null || first.All(pair => second?.GetValueOrDefault(pair.Key) == pair.Value));

    private sealed record FilterChoice(Guid? Id, string Name);

    private sealed record ComponentRow(Equipment Item, Assignment Assignment)
    {
        public string Name => Item.ToString();
        public string Role => Assignment.Role == AstroRig.Role.FilterWheel ? "Filter Wheel" : Assignment.Role.ToString();
        public double? Weight => Item.WeightKg > 0 ? Item.WeightKg : null;
        public bool Payload => Assignment.CountInPayload;
        public bool Backfocus => Assignment.CountInBackfocus;
    }
}
