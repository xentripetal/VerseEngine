// See https://aka.ms/new-console-template for more information

using ProjectVerse;
using Serilog;
using Verse.Core;

Log.Logger = new LoggerConfiguration()
	.WriteTo.Console()
	.CreateLogger();
var app = App.Default();
app.World.Entity().Set((int)1); //.Set(new List<int>());
app.World.Entity().Set(true).Set(2);

app.AddSchedulable(new RunSystems());

app.Update();
app.Update();
app.Update();
app.Update();
app.Update();
