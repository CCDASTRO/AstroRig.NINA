using System.ComponentModel;

namespace AstroRig;

internal sealed class EquipmentFields(Equipment item, bool adding = false) : CustomTypeDescriptor
{
    private static readonly HashSet<string> Common =
    [
        nameof(Equipment.Brand), nameof(Equipment.Model), nameof(Equipment.Category), nameof(Equipment.WeightKg),
        nameof(Equipment.Status), nameof(Equipment.PurchaseCost), nameof(Equipment.Notes)
    ];

    public override PropertyDescriptorCollection GetProperties() => GetProperties(null);

    public override PropertyDescriptorCollection GetProperties(Attribute[]? attributes)
    {
        string[] visible = item.Category switch
        {
            Role.Mount => [nameof(Equipment.MountType), nameof(Equipment.RatedPayloadKg), nameof(Equipment.ImagingTargetKg),
                nameof(Equipment.PowerConnection)],
            Role.Optics => [nameof(Equipment.ApertureMm), nameof(Equipment.FocalLengthMm), nameof(Equipment.ImageCircleMm),
                nameof(Equipment.OpticalDesign), nameof(Equipment.OutputThread)],
            Role.MainCamera => [nameof(Equipment.PixelSizeUm), nameof(Equipment.SensorWidthMm),
                nameof(Equipment.SensorHeightMm), nameof(Equipment.ResolutionX), nameof(Equipment.ResolutionY),
                nameof(Equipment.Cooled), nameof(Equipment.BuiltInGuider), nameof(Equipment.PowerConnection),
                nameof(Equipment.SpacingMm), nameof(Equipment.InputThread), nameof(Equipment.OutputThread)],
            Role.GuideCamera => [nameof(Equipment.PixelSizeUm), nameof(Equipment.SensorWidthMm), nameof(Equipment.SensorHeightMm),
                nameof(Equipment.ResolutionX), nameof(Equipment.ResolutionY), nameof(Equipment.PowerConnection),
                nameof(Equipment.SpacingMm), nameof(Equipment.OutputThread)],
            Role.GuideScope when item.GuideDeviceType == GuideDeviceKind.OffAxisGuider => [nameof(Equipment.GuideDeviceType),
                nameof(Equipment.SpacingMm), nameof(Equipment.InputThread), nameof(Equipment.OutputThread)],
            Role.GuideScope => [nameof(Equipment.GuideDeviceType), nameof(Equipment.ApertureMm), nameof(Equipment.FocalLengthMm),
                nameof(Equipment.OutputThread)],
            Role.Reducer => [nameof(Equipment.FocalMultiplier), nameof(Equipment.ImageCircleMm), nameof(Equipment.RequiredBackfocusMm),
                nameof(Equipment.InputThread), nameof(Equipment.OutputThread)],
            Role.Filter => [nameof(Equipment.FilterSpecification), nameof(Equipment.SpacingMm), nameof(Equipment.InputThread),
                nameof(Equipment.OutputThread), nameof(Equipment.PowerConnection)],
            Role.FilterWheel => [nameof(Equipment.FilterSlotCount), nameof(Equipment.FilterSpecification),
                nameof(Equipment.SpacingMm), nameof(Equipment.InputThread), nameof(Equipment.OutputThread),
                nameof(Equipment.PowerConnection)],
            Role.TiltPlate or Role.Focuser => [nameof(Equipment.AdjustmentType), nameof(Equipment.TravelMm),
                nameof(Equipment.SpacingMm), nameof(Equipment.InputThread), nameof(Equipment.OutputThread), nameof(Equipment.PowerConnection)],
            Role.Accessory => [nameof(Equipment.SpacingMm), nameof(Equipment.InputThread), nameof(Equipment.OutputThread), nameof(Equipment.PowerConnection)],
            Role.Controller or Role.Power => [nameof(Equipment.PowerConnection)],
            _ => Array.Empty<string>()
        };

        var properties = TypeDescriptor.GetProperties(item, attributes)
            .Cast<PropertyDescriptor>()
            .Where(property => Common.Contains(property.Name) || visible.Contains(property.Name))
            .Where(property => !adding || property.Name is not (nameof(Equipment.Category) or nameof(Equipment.Model)))
            .Select(property => property.Name == nameof(Equipment.SpacingMm) &&
                item.Category is Role.MainCamera or Role.GuideCamera
                ? TypeDescriptor.CreateProperty(typeof(Equipment), property, new DisplayNameAttribute("Sensor backfocus (mm)"))
                : property)
            .ToArray();
        return new PropertyDescriptorCollection(properties, true);
    }

    internal static string SpecsTooltip(Equipment item, string? slotSummary)
    {
        var lines = new List<string> { item.ToString() };
        foreach (PropertyDescriptor property in new EquipmentFields(item).GetProperties())
        {
            if (Common.Contains(property.Name) && property.Name != nameof(Equipment.WeightKg) ||
                property.Name == nameof(Equipment.FilterSlotCount))
                continue;

            var value = property.GetValue(item) switch
            {
                string text when !string.IsNullOrWhiteSpace(text) => text.Trim(),
                double number when double.IsFinite(number) && number > 0 => number.ToString("0.###"),
                int count when count > 0 => count.ToString(),
                true => "Yes",
                GuideDeviceKind.OffAxisGuider => "Off-axis guider",
                GuideDeviceKind.GuideScope => "Guide scope",
                _ => null
            };
            if (value != null) lines.Add($"{property.DisplayName}: {value}");
        }

        if (slotSummary != null) lines.Add(slotSummary);
        if (lines.Count == 1) lines.Add("No specs recorded.");
        return string.Join(Environment.NewLine, lines);
    }

    public override object GetPropertyOwner(PropertyDescriptor? pd) => item;

    public static string TypeLabel(Role role) => role switch
    {
        Role.Optics => "Telescope / Lens",
        Role.MainCamera => "Main Camera",
        Role.GuideScope => "Guide Scope / OAG",
        Role.GuideCamera => "Guide Camera",
        Role.Reducer => "Reducer / Barlow / Flattener",
        Role.FilterWheel => "Filter Wheel",
        Role.TiltPlate => "Tilt Plate",
        Role.Power => "Power / Dew Control",
        Role.Tripod => "Tripod / Pier",
        _ => role.ToString()
    };
}
