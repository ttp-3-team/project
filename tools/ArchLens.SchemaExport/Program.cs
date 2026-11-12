using ArchLens.Domain.Model;

var path = args.Length == 1 ? args[0] : "packages/contracts/model.schema.json";
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
File.WriteAllText(path, ModelSchema.Export());
Console.WriteLine($"wrote {path}");
