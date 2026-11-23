using System.ComponentModel.Composition;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.Plugin;
using NINA.Plugin.Interfaces;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.ViewModel;

[assembly: Guid("b5be0195-e556-4066-86a6-f763aceb33ab")]
[assembly: AssemblyMetadata("MinimumApplicationVersion", "3.3.0.1069")]
[assembly: AssemblyMetadata("License", "MPL-2.0")]
[assembly: AssemblyMetadata("LicenseURL", "https://www.mozilla.org/MPL/2.0/")]
[assembly: AssemblyMetadata("Repository", "https://github.com/CCDASTRO/AstroRig.NINA")]
[assembly: AssemblyMetadata("FeaturedImageURL", "https://github.com/CCDASTRO/AstroRig/releases/download/v1.0.11/AstroRig-Plugin-Icon.png")]
[assembly: AssemblyMetadata("Homepage", "https://github.com/CCDASTRO/AstroRig.NINA#readme")]
[assembly: AssemblyMetadata("ChangelogURL", "https://github.com/CCDASTRO/AstroRig.NINA/releases/tag/v1.0.4")]
[assembly: AssemblyMetadata("Tags", "Equipment,Rig Planning,Inventory")]
[assembly: AssemblyMetadata("LongDescription", @"AstroRig keeps one equipment inventory for multiple imaging rigs. Review payload, backfocus, image scale, and field of view. See occupied filter-wheel slots in the rig table, hover over equipment for its saved specs, and export to Excel. To start, open Imaging > AstroRig and create an inventory or open the same JSON file used by standalone AstroRig. Download the plugin ZIP from the homepage or release notes, extract it into %LOCALAPPDATA%\NINA\Plugins\3.0.0\AstroRig with N.I.N.A. closed, then restart.")]
[assembly: AssemblyMetadata("ScreenshotURL", "https://github.com/CCDASTRO/AstroRig.NINA/releases/download/v1.0.3/AstroRig-Showcase.png")]

namespace AstroRig.NinaPlugin;

[Export(typeof(IPluginManifest))]
public sealed class AstroRigPlugin : PluginBase
{
}

[Export(typeof(IDockableVM))]
public sealed class AstroRigDockable : DockableVM
{
    [ImportingConstructor]
    public AstroRigDockable(IProfileService profiles) : base(profiles)
    {
        Title = "AstroRig";
        ImageGeometry = new GeometryGroup();
        ImageGeometry.Children.Add(Geometry.Parse("M2,2 H22 V22 H2 Z M4,7 H20 M4,12 H20 M4,17 H20"));
        ImageGeometry.Freeze();
    }
}

[Export(typeof(ResourceDictionary))]
public partial class AstroRigTemplates : ResourceDictionary
{
    public AstroRigTemplates() => InitializeComponent();
}