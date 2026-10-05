using System;
using System.IO;
using BombArena.Core;

// Generates Game/Assets/Resources/Stages/stage-001.txt … stage-100.txt from StageTable.
// Existing files are overwritten only with --force, so hand-tuned stages are not lost by accident.
bool force = Array.IndexOf(args, "--force") >= 0;
var root = AppContext.BaseDirectory;
while (root != null && !Directory.Exists(Path.Combine(root, "Game", "Assets")))
    root = Path.GetDirectoryName(root.TrimEnd(Path.DirectorySeparatorChar));
if (root == null) { Console.Error.WriteLine("Run from inside the repository."); return 1; }

var dir = Path.Combine(root, "Game", "Assets", "Resources", "Stages");
Directory.CreateDirectory(dir);
int written = 0, kept = 0;
for (int n = 1; n <= StageTable.StageCount; n++)
{
    var path = Path.Combine(dir, StageFile.FileName(n) + ".txt");
    if (File.Exists(path) && !force) { kept++; continue; }
    File.WriteAllText(path, StageFile.Format(StageTable.Spec(n)));
    written++;
}
Console.WriteLine($"Wrote {written} stage files to {dir}; kept {kept} existing (use --force to overwrite).");
return 0;
