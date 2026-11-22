using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;

namespace AstroRig;

internal static class ExcelExport
{
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRel = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace Content = "http://schemas.openxmlformats.org/package/2006/content-types";

    public static void Write(string path, Inventory inventory)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = File.Create(temporary))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
                WritePackage(zip, inventory);
            File.Move(temporary, path, true);
        }
        finally { File.Delete(temporary); }
    }

    private static void WritePackage(ZipArchive zip, Inventory inventory)
    {
        string[] names = ["Rigs", "Equipment", "Rig Components"];
        XElement Link(int id, string type, string target) => new(PackageRel + "Relationship",
            new XAttribute("Id", $"rId{id}"), new XAttribute("Type", Rel.NamespaceName + "/" + type), new XAttribute("Target", target));
        XElement Override(string path, string type) => new(Content + "Override",
            new XAttribute("PartName", path), new XAttribute("ContentType", $"application/vnd.openxmlformats-officedocument.spreadsheetml.{type}+xml"));

        Save(zip, "[Content_Types].xml", new XElement(Content + "Types",
            new XElement(Content + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
            Override("/xl/workbook.xml", "sheet.main"), Override("/xl/styles.xml", "styles"),
            names.Select((_, i) => Override($"/xl/worksheets/sheet{i + 1}.xml", "worksheet"))));
        Save(zip, "_rels/.rels", new XElement(PackageRel + "Relationships", Link(1, "officeDocument", "xl/workbook.xml")));
        Save(zip, "xl/_rels/workbook.xml.rels", new XElement(PackageRel + "Relationships",
            names.Select((_, i) => Link(i + 1, "worksheet", $"worksheets/sheet{i + 1}.xml")), Link(4, "styles", "styles.xml")));
        Save(zip, "xl/workbook.xml", new XElement(Main + "workbook", new XAttribute(XNamespace.Xmlns + "r", Rel.NamespaceName),
            new XElement(Main + "bookViews", new XElement(Main + "workbookView", new XAttribute("activeTab", 0))),
            new XElement(Main + "sheets", names.Select((name, i) => new XElement(Main + "sheet",
                new XAttribute("name", name), new XAttribute("sheetId", i + 1), new XAttribute(Rel + "id", $"rId{i + 1}"))))));
        Save(zip, "xl/styles.xml", XElement.Parse(
            """
            <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
              <fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><color rgb="FFFFFFFF"/><sz val="11"/><name val="Calibri"/></font></fonts>
              <fills count="3"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill><fill><patternFill patternType="solid"><fgColor rgb="FF263A54"/><bgColor indexed="64"/></patternFill></fill></fills>
              <borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>
              <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
              <cellXfs count="3"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0" applyFont="1" applyFill="1"/><xf numFmtId="2" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/></cellXfs>
              <cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>
            </styleSheet>
            """));

        Sheet(zip, 1, ["Rig", "Mount", "Main optics", "Main camera", "Binning", "Focal length (mm)", "Focal ratio",
            "Image scale (arcsec/px)", "Horizontal FOV (°)", "Vertical FOV (°)", "Recorded payload (kg)", "Missing weights",
            "Rated payload (kg)", "Imaging target (kg)", "Payload headroom (kg)", "Required backfocus (mm)",
            "Recorded spacing (mm)", "Backfocus remaining (mm)", "Notes"], inventory.Rigs.Select(rig =>
        {
            var entries = inventory.Resolve(rig).ToArray();
            string? Item(Role role) => entries.FirstOrDefault(x => x.Assignment.Role == role).Item?.ToString();
            var m = RigMetrics.Calculate(inventory, rig);
            return new object?[] { rig.Name, Item(Role.Mount), Item(Role.Optics), Item(Role.MainCamera), rig.Binning, m.FocalLength,
                m.FocalRatio, m.ImageScale, m.WidthDegrees, m.HeightDegrees, m.Payload, m.MissingWeights, m.RatedPayload, m.ImagingTarget,
                m.Headroom, m.RequiredBackfocus, m.Spacing, m.Remaining, rig.Notes };
        }));
        Sheet(zip, 2, ["Equipment ID", "Brand", "Model", "Category", "Weight (kg)", "Status", "Purchase cost", "Notes",
            "Aperture (mm)", "Focal length (mm)", "Focal multiplier", "Image circle (mm)", "Pixel size (µm)", "Sensor width (mm)",
            "Sensor height (mm)", "Resolution X", "Resolution Y", "Spacing (mm)", "Input thread", "Output thread", "Rated payload (kg)",
            "Imaging target (kg)", "Mount type", "Optical design", "Guide device type", "Cooled camera", "Built-in guider",
            "Required backfocus (mm)", "Adjustment type", "Travel (mm)", "Filter size / bandpass", "Power / data connection"],
            inventory.Equipment.Select(e => new object?[] { e.Id.ToString(), e.Brand, e.Model, e.Category.ToString(),
                Optional(e.WeightKg), e.Status, Optional(e.PurchaseCost), e.Notes, Optional(e.ApertureMm), Optional(e.FocalLengthMm),
                e.Category == Role.Reducer ? e.FocalMultiplier : null, Optional(e.ImageCircleMm), Optional(e.PixelSizeUm),
                Optional(e.SensorWidthMm), Optional(e.SensorHeightMm), Optional(e.ResolutionX), Optional(e.ResolutionY), Optional(e.SpacingMm),
                e.InputThread, e.OutputThread, Optional(e.RatedPayloadKg), Optional(e.ImagingTargetKg),
                e.MountType, e.OpticalDesign, e.Category == Role.GuideScope ? e.GuideDeviceType.ToString() : null,
                e.Category == Role.MainCamera ? e.Cooled : null, e.Category == Role.MainCamera ? e.BuiltInGuider : null,
                Optional(e.RequiredBackfocusMm), e.AdjustmentType, Optional(e.TravelMm), e.FilterSpecification, e.PowerConnection }));
        Sheet(zip, 3, ["Rig", "Position", "Equipment ID", "Equipment", "Role", "Weight (kg)", "Count in payload",
            "Spacing (mm)", "Count in backfocus", "Notes"], inventory.Rigs.SelectMany(rig => inventory.Resolve(rig)
            .Select((x, i) => new object?[] { rig.Name, i + 1, x.Item.Id.ToString(), x.Item.ToString(), x.Assignment.Role.ToString(),
                Optional(x.Item.WeightKg), x.Assignment.CountInPayload, x.Item.SpacingMm, x.Assignment.CountInBackfocus, x.Assignment.Notes })));
    }

    private static double? Optional(double value) => value == 0 ? null : value;

    private static void Sheet(ZipArchive zip, int index, string[] headers, IEnumerable<object?[]> data)
    {
        var rows = data.Prepend(headers.Cast<object?>().ToArray()).ToArray();
        var columns = headers.Select((h, i) => new XElement(Main + "col", new XAttribute("min", i + 1), new XAttribute("max", i + 1),
            new XAttribute("width", Math.Clamp(Math.Max(h.Length + 5, rows.Skip(1).Select(row => row[i]?.ToString()?.Length + 2 ?? 0).DefaultIfEmpty(0).Max()), 14, 46)),
            new XAttribute("customWidth", 1)));
        Save(zip, $"xl/worksheets/sheet{index}.xml", new XElement(Main + "worksheet",
            new XElement(Main + "sheetViews", new XElement(Main + "sheetView", new XAttribute("workbookViewId", 0),
                new XElement(Main + "pane", new XAttribute("ySplit", 1), new XAttribute("topLeftCell", "A2"),
                    new XAttribute("activePane", "bottomLeft"), new XAttribute("state", "frozen")))),
            new XElement(Main + "cols", columns),
            new XElement(Main + "sheetData", rows.Select((row, i) => new XElement(Main + "row",
                new XAttribute("r", i + 1), row.Select((value, col) => Cell(value, $"{ColumnName(col + 1)}{i + 1}", i == 0))))),
            new XElement(Main + "autoFilter", new XAttribute("ref", $"A1:{ColumnName(headers.Length)}{rows.Length}"))));
    }

    private static string ColumnName(int number)
    {
        var name = "";
        while (number > 0)
        {
            number--;
            name = (char)('A' + number % 26) + name;
            number /= 26;
        }
        return name;
    }

    private static XElement? Cell(object? value, string address, bool header)
    {
        if (value is null) return null;
        var cell = new XElement(Main + "c", new XAttribute("r", address));
        if (header) cell.Add(new XAttribute("s", 1));
        if (value is double number && double.IsFinite(number))
        {
            cell.Add(new XAttribute("s", 2), new XElement(Main + "v", number.ToString("G17", CultureInfo.InvariantCulture)));
        }
        else if (value is int integer) cell.Add(new XElement(Main + "v", integer));
        else if (value is bool flag) cell.Add(new XAttribute("t", "b"), new XElement(Main + "v", flag ? 1 : 0));
        else
        {
            var raw = value.ToString() ?? "";
            // Inline strings preserve literal values even when they start with an Excel formula marker.
            var normalized = string.Concat(raw.EnumerateRunes().Where(rune => rune.Value is 9 or 10 or 13 or >= 32 and <= 0xD7FF
                or >= 0xE000 and <= 0xFFFD or >= 0x10000).Select(rune => rune.ToString()));
            cell.Add(new XAttribute("t", "inlineStr"), new XElement(Main + "is", new XElement(Main + "t",
                new XAttribute(XNamespace.Xml + "space", "preserve"), normalized)));
        }
        return cell;
    }

    private static void Save(ZipArchive zip, string path, XElement content)
    {
        using var output = zip.CreateEntry(path, CompressionLevel.Optimal).Open();
        content.Save(output);
    }
}
