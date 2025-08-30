// See https://aka.ms/new-console-template for more information

using Serilog;
using Verse.Core;
using Verse.MoonWorks;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
/**
var app = App.Default();
app.World.Entity().Set((int)1); //.Set(new List<int>());
app.World.Entity().Set(true).Set(2);

app.AddSchedulable(new RunSystems());

app.Update();
app.Update();
app.Update();
app.Update();
app.Update();
**/

var app = App.Default();
app.AddPlugin(new MoonWorksPlugin(new AppInfo("Xentripetal", "ProjectVerse")));
app.World.Entity().Set(new WindowComponent(new Window(new WindowCreateInfo("Test", 1080, 720, ScreenMode.Windowed), 0)));
app.World.Entity().Set(new WindowComponent(new Window(new WindowCreateInfo("Test", 1080, 720, ScreenMode.Windowed), 0)));
app.World.Entity().Set(new WindowComponent(new Window(new WindowCreateInfo("Test", 1080, 720, ScreenMode.Windowed), 0)));
app.Run();
Log.CloseAndFlush();