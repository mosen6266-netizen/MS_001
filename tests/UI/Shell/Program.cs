using Mosen.GroupScheduler.Core.Shell;
int passed = 0;
void Check(bool ok, string message) { if (!ok) throw new Exception("FAIL " + message); passed++; Console.WriteLine("PASS " + message); }
var screen = new DisplayArea(0, 0, 1920, 1080);
var fresh = WindowPlacementPolicy.Normalize(null, screen);
Check(fresh.Width == 1180 && fresh.Height == 760, "default window size");
Check(fresh.Left == 370 && fresh.Top == 160, "initial window centered");
var moved = WindowPlacementPolicy.Normalize(new(-10000, 9000, 1180, 760, true), screen);
Check(moved.Left == 0 && moved.Top == 320, "offscreen coordinates clamped");
Check(moved.Maximized, "maximized state preserved");
var oversized = WindowPlacementPolicy.Normalize(new(0, 0, 99999, 99999, false), new(0, 0, 1100, 720));
Check(oversized.Width == 1100 && oversized.Height == 720, "window fits smaller display");
var bad = WindowPlacementPolicy.Normalize(new(double.NaN, double.PositiveInfinity, double.NaN, -1, false), screen);
Check(double.IsFinite(bad.Left) && double.IsFinite(bad.Top), "invalid stored coordinates reset");
Check(bad.Width == 1180 && bad.Height == 420, "invalid dimensions guarded");
var secondary = WindowPlacementPolicy.Normalize(new(-1800, 400, 900, 600, false), new(-1920, 0, 1920, 1080));
Check(secondary.Left == -1800 && secondary.Top == 400, "negative monitor coordinates valid");
var tiny = WindowPlacementPolicy.Normalize(null, new(10, 20, 500, 300));
Check(tiny.Left >= 10 && tiny.Top >= 20 && tiny.Width == 500, "small virtual screen safe");
bool invalid = false;
try { WindowPlacementPolicy.Normalize(null, new(0, 0, 0, 0)); } catch (ArgumentOutOfRangeException) { invalid = true; }
Check(invalid, "invalid display bounds rejected");
Console.WriteLine("SUMMARY: " + passed + " geometry tests passed.");
