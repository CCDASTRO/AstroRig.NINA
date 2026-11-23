using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace AstroRig.NinaPlugin;

public partial class RigPanel
{
    private void NewInventory(object sender, RoutedEventArgs args)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "AstroRig inventory (*.json)|*.json",
            FileName = "AstroRig inventory.json",
            DefaultExt = ".json"
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        try
        {
            using (var stream = new FileStream(dialog.FileName, FileMode.CreateNew, FileAccess.Write))
                JsonSerializer.Serialize(stream, new Inventory(), new JsonSerializerOptions { WriteIndented = true });
            OpenAndRemember(dialog.FileName);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Status.Text = $"Could not create a new inventory. Choose a new filename. {error.Message}";
        }
    }

    private void AddEquipment(object sender, RoutedEventArgs args)
    {
        if (!CheckInventory()) return;
        var type = RolePicker();
        if (!EditDetails("Choose equipment type", panel => AddField(panel, "Equipment type", type), () => null)) return;
        var item = new Equipment { Category = (Role)type.SelectedValue };
        var fields = new List<(PropertyDescriptor Property, Control Editor)>();
        if (!EditDetails($"Add {EquipmentFields.TypeLabel(item.Category)}", panel =>
        {
            foreach (PropertyDescriptor property in new EquipmentFields(item).GetProperties())
            {
                if (property.Name == nameof(Equipment.Category)) continue;
                var value = property.GetValue(item);
                Control editor = value switch
                {
                    bool flag => new CheckBox { IsChecked = flag, VerticalAlignment = VerticalAlignment.Center },
                    Enum choice => new ComboBox { ItemsSource = Enum.GetValues(choice.GetType()), SelectedItem = choice },
                    _ => new TextBox { Text = Convert.ToString(value, CultureInfo.CurrentCulture) ?? "" }
                };
                fields.Add((property, editor));
                AddField(panel, property.DisplayName, editor);
            }
        }, () =>
        {
            foreach (var (property, editor) in fields)
            {
                object? value;
                if (editor is CheckBox check) value = check.IsChecked == true;
                else if (editor is ComboBox combo) value = combo.SelectedItem;
                else
                {
                    var input = ((TextBox)editor).Text.Trim();
                    if (property.PropertyType == typeof(double))
                    {
                        if (!double.TryParse(input, out var number) || !double.IsFinite(number) || number < 0 ||
                            (property.Name == nameof(Equipment.FocalMultiplier) && number == 0))
                            return $"Enter a valid value for {property.DisplayName}.";
                        value = number;
                    }
                    else if (property.PropertyType == typeof(int))
                    {
                        if (!int.TryParse(input, out var number) || number < 0 ||
                            (property.Name == nameof(Equipment.FilterSlotCount) && number is < 1 or > 64))
                            return $"Enter a valid value for {property.DisplayName}. Filter wheels support 1–64 slots.";
                        value = number;
                    }
                    else value = input;
                }
                property.SetValue(item, value);
            }
            return string.IsNullOrWhiteSpace(item.Model) ? "Enter a model name." : null;
        })) return;

        SaveChange(latest =>
        {
            latest.Equipment.Add(item);
            return null;
        }, $"Added {item} to inventory. Use Assign equipment to add it to a rig.");
    }

    private void AddRig(object sender, RoutedEventArgs args) => EditRig(null);
    private void EditSelectedRig(object sender, RoutedEventArgs args)
    {
        if (Rigs.SelectedItem is Rig rig) EditRig(rig);
        else Status.Text = "Select a rig first.";
    }

    private void EditRig(Rig? rig)
    {
        if (!CheckInventory()) return;
        var name = new TextBox { Text = rig?.Name ?? "" };
        var notes = new TextBox { Text = rig?.Notes ?? "" };
        var binning = new TextBox { Text = (rig?.Binning ?? 1).ToString(CultureInfo.CurrentCulture) };
        var backfocus = new TextBox { Text = (rig?.RequiredBackfocusMm ?? 0).ToString(CultureInfo.CurrentCulture) };
        if (!EditDetails(rig == null ? "Add rig" : "Edit rig", panel =>
        {
            AddField(panel, "Rig name", name);
            AddField(panel, "Sensor binning", binning);
            AddField(panel, "Required backfocus (mm)", backfocus);
            AddField(panel, "Notes", notes);
        }, () =>
        {
            if (string.IsNullOrWhiteSpace(name.Text)) return "Enter a rig name.";
            if (!int.TryParse(binning.Text, out var bins) || bins < 1) return "Binning must be at least 1.";
            if (!double.TryParse(backfocus.Text, out var spacing) || !double.IsFinite(spacing) || spacing < 0)
                return "Enter a non-negative backfocus in millimeters.";
            return null;
        })) return;

        var rigName = name.Text.Trim();
        SaveChange(latest =>
        {
            var current = rig == null ? new Rig() : latest.Rigs.Find(candidate => candidate.Name == rig.Name);
            if (current == null || (rig != null && (current.Notes != rig.Notes || current.Binning != rig.Binning ||
                current.RequiredBackfocusMm != rig.RequiredBackfocusMm))) return "The rig changed on disk. Reload and try again.";
            if (latest.Rigs.Any(candidate => candidate != current && string.Equals(candidate.Name, rigName, StringComparison.OrdinalIgnoreCase)))
                return "A rig with that name already exists.";
            current.Name = rigName;
            current.Notes = notes.Text.Trim();
            current.Binning = int.Parse(binning.Text, CultureInfo.CurrentCulture);
            current.RequiredBackfocusMm = double.Parse(backfocus.Text, CultureInfo.CurrentCulture);
            if (rig == null) latest.Rigs.Add(current);
            return null;
        }, $"Saved rig {rigName}.", rigName);
    }

    private void AssignEquipment(object sender, RoutedEventArgs args)
    {
        if (inventory == null || Rigs.SelectedItem is not Rig rig)
        {
            Status.Text = "Add or select a rig first.";
            return;
        }
        var available = inventory.Equipment.OrderBy(item => item.ToString(), StringComparer.CurrentCultureIgnoreCase).ToArray();
        if (available.Length == 0)
        {
            Status.Text = "Add equipment to the inventory first.";
            return;
        }
        var picker = new ComboBox { ItemsSource = available, SelectedIndex = 0 };
        if (!EditDetails("Assign equipment", panel => AddField(panel, "Equipment", picker), () => null)) return;
        var selected = (Equipment)picker.SelectedItem;
        SaveChange(latest =>
        {
            var current = latest.Rigs.Find(candidate => candidate.Name == rig.Name);
            var item = latest.Equipment.Find(candidate => candidate.Id == selected.Id);
            if (current == null || item == null) return "The rig or equipment changed on disk. Reload and try again.";
            current.Items.Add(Assignment.For(item));
            return null;
        }, $"Assigned {selected} to {rig}.");
    }

    private void EditComponent(object sender, RoutedEventArgs args)
    {
        if (Rigs.SelectedItem is not Rig rig || Components.SelectedItem is not ComponentRow row)
        {
            Status.Text = "Select a component in the rig table.";
            return;
        }
        var role = RolePicker(row.Assignment.Role);
        var payload = new CheckBox { IsChecked = row.Payload };
        var backfocus = new CheckBox { IsChecked = row.Backfocus };
        var notes = new TextBox { Text = row.Assignment.Notes };
        if (!EditDetails($"Edit {row.Name} assignment", panel =>
        {
            AddField(panel, "Role", role);
            AddField(panel, "Count in payload", payload);
            AddField(panel, "Count in backfocus", backfocus);
            AddField(panel, "Notes", notes);
        }, () => null)) return;

        ChangeComponent(rig, assignments =>
        {
            var current = assignments[rig.Items.IndexOf(row.Assignment)];
            current.Role = (Role)role.SelectedValue;
            current.CountInPayload = payload.IsChecked == true;
            current.CountInBackfocus = backfocus.IsChecked == true;
            current.Notes = notes.Text.Trim();
        }, $"Updated {row.Name}.");
    }

    private void RemoveComponent(object sender, RoutedEventArgs args)
    {
        if (Rigs.SelectedItem is not Rig rig || Components.SelectedItem is not ComponentRow row)
        {
            Status.Text = "Select a component in the rig table.";
            return;
        }
        if (MessageBox.Show(Window.GetWindow(this), $"Remove {row.Name} from {rig}?", "AstroRig",
            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        ChangeComponent(rig, assignments => assignments.RemoveAt(rig.Items.IndexOf(row.Assignment)), $"Removed {row.Name} from {rig}.");
    }

    private void ChangeComponent(Rig rig, Action<List<Assignment>> change, string message)
    {
        SaveChange(latest =>
        {
            var current = latest.Rigs.Find(candidate => candidate.Name == rig.Name);
            if (current == null || !current.Items.Select(AssignmentValues).SequenceEqual(rig.Items.Select(AssignmentValues)))
                return "The rig components changed on disk. Reload and try again.";
            change(current.Items);
            return null;
        }, message);
    }

    private static (Guid, Role, bool, bool, string) AssignmentValues(Assignment item) =>
        (item.EquipmentId, item.Role, item.CountInPayload, item.CountInBackfocus, item.Notes);

    private bool CheckInventory()
    {
        if (inventory != null) return true;
        Status.Text = "Create or open an inventory first.";
        return false;
    }

    private void SaveChange(Func<Inventory, string?> change, string message, string? selectedRig = null)
    {
        if (!CheckInventory()) return;
        try
        {
            if (!File.Exists(path)) throw new FileNotFoundException("The inventory was moved or removed.");
            var latest = Inventory.Load(path);
            var error = change(latest);
            if (error != null)
            {
                Status.Text = error;
                return;
            }
            latest.Save(path);
            LoadInventory(path);
            if (selectedRig != null) Rigs.SelectedItem = inventory?.Rigs.Find(rig => rig.Name == selectedRig);
            Status.Text = $"{message} Saved to {path}";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            Status.Text = $"Could not save inventory. {error.Message}";
        }
    }

    private static ComboBox RolePicker(Role role = Role.Optics) => new()
    {
        ItemsSource = Enum.GetValues<Role>().Select(value => new RoleChoice(value, EquipmentFields.TypeLabel(value))).ToArray(),
        DisplayMemberPath = nameof(RoleChoice.Name), SelectedValuePath = nameof(RoleChoice.Value), SelectedValue = role
    };

    private sealed record RoleChoice(Role Value, string Name);

    private static void AddField(Panel fields, string label, Control editor)
    {
        var row = new DockPanel { Margin = new Thickness(0, 0, 0, 5) };
        row.Children.Add(new Label { Content = label, Width = 190, Target = editor });
        row.Children.Add(editor);
        fields.Children.Add(row);
    }

    private bool EditDetails(string title, Action<StackPanel> addFields, Func<string?> validate)
    {
        var window = new Window
        {
            Owner = Window.GetWindow(this), Title = title, Width = 525, MaxHeight = 620,
            SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false, ResizeMode = ResizeMode.NoResize
        };
        var fields = new StackPanel { Margin = new Thickness(12) };
        addFields(fields);
        var error = new TextBlock { Margin = new Thickness(12, 0, 12, 5), TextWrapping = TextWrapping.Wrap };
        var save = new Button { Content = "OK", IsDefault = true, MinWidth = 74, Margin = new Thickness(0, 0, 8, 0) };
        save.Click += (_, _) =>
        {
            var message = validate();
            if (message == null) window.DialogResult = true;
            else error.Text = message;
        };
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(12)
        };
        buttons.Children.Add(save);
        buttons.Children.Add(new Button { Content = "Cancel", IsCancel = true, MinWidth = 74 });
        var layout = new DockPanel();
        DockPanel.SetDock(buttons, Dock.Bottom);
        layout.Children.Add(buttons);
        DockPanel.SetDock(error, Dock.Bottom);
        layout.Children.Add(error);
        layout.Children.Add(new ScrollViewer { Content = fields, MaxHeight = 455, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        ApplyDialogStyles(window);
        window.Content = layout;
        return window.ShowDialog() == true;
    }
}
