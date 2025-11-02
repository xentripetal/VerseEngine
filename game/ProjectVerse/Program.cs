// See https://aka.ms/new-console-template for more information

using ProjectVerse;
using Serilog;
using Verse.Assets;
using Verse.Core;
using Verse.MoonWorks;
using Verse.Render;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

var app = App.Default();
app.AddPlugin(new MoonWorksPlugin(new AppInfo("Xentripetal", "ProjectVerse")));
app.AddPlugin(new AssetPlugin());
app.AddPlugin(new MoonWorksGraphicsPlugin());
app.AddPlugin(new RenderPlugin());
app.AddSchedulable<RunSystems>();
app.InitResource<MyAssets>();
app.InitResource<Example>();
app.World.Entity().Set(new WindowComponent(new Window(new WindowCreateInfo("Test", 1080, 720, ScreenMode.Windowed), 0)));

app.Run();

Log.CloseAndFlush();