using System.Text.Json;
using System.Text.Json.Serialization;
using System.ComponentModel;

namespace AstroRig;

public enum Role { Mount, Optics, MainCamera, Reducer, Filter, GuideScope, GuideCamera, TiltPlate, Focuser, Controller, Power, Tripod, Accessory, FilterWheel }
public enum GuideDeviceKind { GuideScope, OffAxisGuider }

public sealed class Equipment
{
    [Browsable(false)] public Guid Id { get; set; } = Guid.NewGuid();
    public string Brand { get; set; } = "";
    public string Model { get; set; } = "";
    [DisplayName("Equipment type")] public Role Category { get; set; } = Role.Accessory;
    [Category("General"), DisplayName("Weight (kg)")] public double WeightKg { get; set; }
    public string Status { get; set; } = "Owned";
    public double PurchaseCost { get; set; }
    public string Notes { get; set; } = "";
    [Category("Optical specs"), DisplayName("Aperture (mm)")] public double ApertureMm { get; set; }
    [Category("Optical specs"), DisplayName("Focal length (mm)")] public double FocalLengthMm { get; set; }
    [Category("Optical specs"), DisplayName("Focal length multiplier")] public double FocalMultiplier { get; set; } = 1;
    [Category("Optical specs"), DisplayName("Image circle (mm)")] public double ImageCircleMm { get; set; }
    [Category("Sensor"), DisplayName("Pixel size (µm)")] public double PixelSizeUm { get; set; }
    [Category("Sensor"), DisplayName("Sensor width (mm)")] public double SensorWidthMm { get; set; }
    [Category("Sensor"), DisplayName("Sensor height (mm)")] public double SensorHeightMm { get; set; }
    [Category("Sensor"), DisplayName("Resolution X (px)")] public int ResolutionX { get; set; }
    [Category("Sensor"), DisplayName("Resolution Y (px)")] public int ResolutionY { get; set; }
    [Category("Connections"), DisplayName("Spacing contribution (mm)")] public double SpacingMm { get; set; }
    [Category("Connections"), DisplayName("Input thread")] public string InputThread { get; set; } = "";
    [Category("Connections"), DisplayName("Output thread")] public string OutputThread { get; set; } = "";
    [Category("Connections"), DisplayName("Power / data connection")] public string PowerConnection { get; set; } = "";
    [Category("Optical specs"), DisplayName("Optical design")] public string OpticalDesign { get; set; } = "";
    [Category("Optical specs"), DisplayName("Guide device type")] public GuideDeviceKind GuideDeviceType { get; set; }
    [Category("Optical specs"), DisplayName("Filter size / bandpass")] public string FilterSpecification { get; set; } = "";
    [Category("Filter wheel"), DisplayName("Number of slots")] public int FilterSlotCount { get; set; }
    [Browsable(false), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<int, Guid>? FilterSlots { get; set; }
    [Category("Sensor"), DisplayName("Cooled camera")] public bool Cooled { get; set; }
    [Category("Sensor"), DisplayName("Built-in guide sensor")] public bool BuiltInGuider { get; set; }
    [Category("Connections"), DisplayName("Required backfocus (mm)")] public double RequiredBackfocusMm { get; set; }
    [Category("Adjustment"), DisplayName("Adjustment type")] public string AdjustmentType { get; set; } = "";
    [Category("Adjustment"), DisplayName("Travel (mm)")] public double TravelMm { get; set; }
    [Category("Mount limits"), DisplayName("Mount type")] public string MountType { get; set; } = "";
    [Category("Mount limits"), DisplayName("Rated payload (kg)")] public double RatedPayloadKg { get; set; }
    [Category("Mount limits"), DisplayName("Imaging target (kg)")] public double ImagingTargetKg { get; set; }
    [Browsable(false)] public string ImportedSpecs { get; set; } = "";
    public override string ToString() => string.Join(" ", new[] { Brand, Model }.Where(s => !string.IsNullOrWhiteSpace(s)));
}

public sealed class Assignment
{
    public Guid EquipmentId { get; set; }
    public Role Role { get; set; }
    public bool CountInPayload { get; set; }
    public bool CountInBackfocus { get; set; }
    public string Notes { get; set; } = "";

    public static Assignment For(Equipment item, bool mainTrain = false) => new()
    {
        EquipmentId = item.Id,
        Role = item.Category,
        CountInPayload = item.Category is not (Role.Mount or Role.Tripod),
        CountInBackfocus = item.Category is Role.MainCamera or Role.TiltPlate or Role.FilterWheel ||
            item.Category == Role.GuideScope && item.GuideDeviceType == GuideDeviceKind.OffAxisGuider ||
            mainTrain && item.Category is not (Role.Optics or Role.Mount or Role.Reducer or Role.GuideScope or Role.GuideCamera)
    };
}

public sealed class Rig
{
    public string Name { get; set; } = "";
    public string Notes { get; set; } = "";
    public int Binning { get; set; } = 1;
    public double RequiredBackfocusMm { get; set; }
    public List<Assignment> Items { get; set; } = [];
    public override string ToString() => Name;
}

public sealed class Inventory
{
    [JsonRequired]
    public List<Equipment> Equipment { get; set; } = [];
    [JsonRequired]
    public List<Rig> Rigs { get; set; } = [];
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public static Inventory Load(string path)
    {
        if (!File.Exists(path)) return new();
        var inventory = JsonSerializer.Deserialize<Inventory>(File.ReadAllText(path), Options);
        if (inventory?.Equipment == null || inventory.Rigs == null ||
            inventory.Rigs.Any(rig => rig == null || rig.Items == null || rig.Items.Any(assignment => assignment == null)) ||
            inventory.Equipment.Any(item => item == null) ||
            inventory.Equipment.Select(item => item.Id).Distinct().Count() != inventory.Equipment.Count)
            throw new InvalidDataException("Invalid AstroRig inventory.");
        return inventory;
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(this, Options));
            File.Move(temporary, path, true);
        }
        finally { File.Delete(temporary); }
    }

    public string? FilterSlotSummary(Equipment item)
    {
        if (item.Category != Role.FilterWheel) return null;
        if (item.FilterSlotCount < 1) return "Filter slot count is not set.";

        var lines = new List<string>();
        if (item.FilterSlots != null)
        {
            foreach (var (slot, id) in item.FilterSlots.OrderBy(pair => pair.Key))
            {
                if (slot < 1 || slot > item.FilterSlotCount) continue;
                var filter = Equipment.Find(candidate => candidate.Id == id && candidate.Category == Role.Filter);
                lines.Add($"Slot {slot}: {filter?.ToString() ?? "Filter unavailable"}");
            }
        }

        lines.Insert(0, $"Filter slots: {lines.Count} of {item.FilterSlotCount} occupied");
        return string.Join(Environment.NewLine, lines);
    }

    public double? RecordedRigCost(Rig rig)
    {
        var assigned = Resolve(rig).Select(pair => pair.Item).DistinctBy(item => item.Id).ToArray();
        var loadedFilterIds = assigned.Where(item => item.Category == Role.FilterWheel && item.FilterSlots != null)
            .SelectMany(wheel => wheel.FilterSlots!.Where(slot => slot.Key >= 1 && slot.Key <= wheel.FilterSlotCount))
            .Select(slot => slot.Value).ToHashSet();
        var priced = assigned.Concat(Equipment.Where(item => item.Category == Role.Filter && loadedFilterIds.Contains(item.Id)))
            .DistinctBy(item => item.Id).Where(item => double.IsFinite(item.PurchaseCost) && item.PurchaseCost > 0).ToArray();
        return priced.Length == 0 ? null : priced.Sum(item => item.PurchaseCost);
    }

    public IEnumerable<(Assignment Assignment, Equipment Item)> Resolve(Rig rig)
    {
        var byId = Equipment.ToDictionary(item => item.Id);
        foreach (var assignment in rig.Items)
            if (byId.TryGetValue(assignment.EquipmentId, out var item))
                yield return (assignment, item);
    }
}

internal sealed record RigMetrics(double? FocalLength, double? FocalRatio, double? ImageScale,
    double? WidthDegrees, double? HeightDegrees, double? Payload, int MissingWeights,
    double? RatedPayload, double? ImagingTarget, double? Headroom, double? RequiredBackfocus, double? Spacing, double? Remaining)
{
    public static RigMetrics Calculate(Inventory inventory, Rig rig)
    {
        var items = inventory.Resolve(rig).ToArray();
        Equipment? Find(Role role) => items.FirstOrDefault(x => x.Assignment.Role == role).Item;
        double? Positive(double? value) => value > 0 ? value : null;
        var optics = Find(Role.Optics);
        var camera = Find(Role.MainCamera);
        var mount = Find(Role.Mount);
        var reducer = Find(Role.Reducer);
        var focal = Positive(optics?.FocalLengthMm) * (Positive(reducer?.FocalMultiplier) ?? 1);
        var required = Positive(rig.RequiredBackfocusMm) ?? Positive(reducer?.RequiredBackfocusMm) ?? Positive(optics?.RequiredBackfocusMm);
        var payloadItems = items.Where(x => x.Assignment.CountInPayload).ToArray();
        var missing = payloadItems.Count(x => x.Item.WeightKg <= 0);
        double? payload = payloadItems.Any(x => x.Item.WeightKg > 0) ? payloadItems.Sum(x => x.Item.WeightKg) : null;
        var rated = Positive(mount?.RatedPayloadKg);
        var target = Positive(mount?.ImagingTargetKg);
        var spacingItems = items.Where(x => x.Assignment.CountInBackfocus).ToArray();
        double? spacing = spacingItems.Length > 0 ? spacingItems.Sum(x => x.Item.SpacingMm) : null;

        double? Field(double? size) => Positive(size) is double mm && focal is double fl
            ? 2 * Math.Atan(mm / (2 * fl)) * 180 / Math.PI : null;
        return new(focal, focal / Positive(optics?.ApertureMm), 206.264806 * Positive(camera?.PixelSizeUm) * Math.Max(1, rig.Binning) / focal,
            Field(camera?.SensorWidthMm), Field(camera?.SensorHeightMm), payload, missing, rated, target,
            missing == 0 ? (target ?? rated) - payload : null, required, spacing, required - spacing);
    }
}
