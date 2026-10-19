using System.CommandLine;
using System.Globalization;

var rootCommand = new RootCommand("");
var bundleCommand = new Command("bundle", "bundle code files to one file");

Option<FileInfo> output = new("--output", "-o")
{
    Description = "the name of the bundle path",
    Required = true,
};

Option<string[]> languages = new("--languages", "-l")
{
    Description = "the languages",
    AllowMultipleArgumentsPerToken = true,
    Required = true,
};

Option<bool> note = new("--note", "-n")
{
    Description = "include the original path"
};

Option<string> sort = new("--sort", "-s")
{
    Description = "Sort files by name or type",
    DefaultValueFactory = _ => "name"
};

Option<bool> remove_empty_lines = new("--remove-empty-lines", "--rel")
{
    Description = "remove empty lines"
};

Option<string> author = new("--author", "--au")
{
    Description = "provides author name"
};

bundleCommand.Add(output);
bundleCommand.Add(languages);
bundleCommand.Add(note);
bundleCommand.Add(sort);
bundleCommand.Add(remove_empty_lines);
bundleCommand.Add(author);

rootCommand.Add(bundleCommand);


bundleCommand.SetAction(parseResult =>
{
    FileInfo? outputPath = parseResult.GetValue(output);
    string[]? selectedLanguages = parseResult.GetValue(languages);
    bool includeNote = parseResult.GetValue(note);
    string sortBy = parseResult.GetValue(sort) ?? "name";
    bool removeEmptyLines = parseResult.GetValue(remove_empty_lines);
    string? authorName = parseResult.GetValue(author);

    if (!sortBy.Equals("name", StringComparison.OrdinalIgnoreCase) &&
        !sortBy.Equals("type", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("שיטת המיון חייבת להיות name או type");
        return;
    }

    if (outputPath == null)
    {
        Console.WriteLine("לא ניתן לכתוב ללא נתיב");
        return;
    }

    if (selectedLanguages == null || selectedLanguages.Length == 0)
    {
        Console.WriteLine("חובה לציין את השפות הנדרשות");
        return;
    }

    string currentDir = Directory.GetCurrentDirectory();
    string outputFullPath;

    try
    {
        outputFullPath = Path.GetFullPath(outputPath.FullName);
    }
    catch (Exception ex) when (
        ex is ArgumentException ||
        ex is NotSupportedException ||
        ex is PathTooLongException)
    {
        Console.WriteLine($"נתיב הפלט אינו תקין: {ex.Message}");
        return;
    }

    var excludedFolders = new[] { "bin", "debug", "obj", ".git" };

    IEnumerable<string> allFiles = Directory
        .EnumerateFiles(currentDir, "*", SearchOption.AllDirectories)
        .Where(file =>
            !file.Split(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar)
            .Any(part => excludedFolders.Contains(
                part, StringComparer.OrdinalIgnoreCase)))
        .Where(file =>
            !string.Equals(
                Path.GetFullPath(file),
                outputFullPath,
                StringComparison.OrdinalIgnoreCase));

    if (!selectedLanguages.Contains(
            "all", StringComparer.OrdinalIgnoreCase))
    {
        var languageExtensions = selectedLanguages
            .Select(lang => lang.StartsWith(".") ? lang : "." + lang)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        allFiles = allFiles.Where(file =>
            languageExtensions.Contains(Path.GetExtension(file)));
    }

    if (sortBy.Equals("name", StringComparison.OrdinalIgnoreCase))
    {
        allFiles = allFiles.OrderBy(
            file => Path.GetFileName(file),
            StringComparer.OrdinalIgnoreCase);
    }
    else
    {
        allFiles = allFiles
            .OrderBy(
                file => Path.GetExtension(file),
                StringComparer.OrdinalIgnoreCase)
            .ThenBy(
                file => Path.GetFileName(file),
                StringComparer.OrdinalIgnoreCase);
    }

    try
    {
        using var writer = new StreamWriter(outputFullPath, false);

        if (!string.IsNullOrWhiteSpace(authorName))
        {
            writer.WriteLine($"// Author: {authorName}");
            writer.WriteLine();
        }

        foreach (string file in allFiles)
        {
            if (includeNote)
            {
                string relativePath = Path.GetRelativePath(currentDir, file);
                writer.WriteLine($"// Source: {relativePath}");
            }

            string content = removeEmptyLines
                ? string.Join(
                    Environment.NewLine,
                    File.ReadAllLines(file)
                        .Where(line => !string.IsNullOrWhiteSpace(line)))
                : File.ReadAllText(file);

            writer.WriteLine(content);
            writer.WriteLine();
        }

        Console.WriteLine($"הקובץ נוצר בהצלחה: {outputFullPath}");
    }
    catch (Exception ex) when (
        ex is IOException ||
        ex is UnauthorizedAccessException ||
        ex is ArgumentException ||
        ex is NotSupportedException)
    {
        Console.WriteLine($"שגיאה ביצירת קובץ ה-bundle: {ex.Message}");
    }
});

var createRspCommand = new Command(
    "create-rsp",
    "Create a response file");

createRspCommand.SetAction(_ =>
{
    Console.WriteLine("Enter the output file name:");
    string? outputPath = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(outputPath))
    {
        Console.WriteLine("Invalid path");
        return;
    }

    try
    {
        outputPath = Path.GetFullPath(outputPath);
    }
    catch (Exception ex) when (
        ex is ArgumentException ||
        ex is NotSupportedException ||
        ex is PathTooLongException)
    {
        Console.WriteLine("Invalid path");
        return;
    }

    Console.WriteLine("Enter the languages:");
    string? languages = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(languages))
    {
        Console.WriteLine("Languages required");
        return;
    }

    var languagesCheck = languages
        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Select(lang => lang.StartsWith(".") ? lang : "." + lang)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    Console.WriteLine("Include the original file path? (true/false)");
    if (!bool.TryParse(Console.ReadLine(), out bool note))
    {
        Console.WriteLine("Invalid input");
        return;
    }

    Console.WriteLine("Enter 'name' or 'type' for sorting (default: name)");
    string? input = Console.ReadLine();
    string sort = string.IsNullOrWhiteSpace(input) ? "name" : input;

    if (!sort.Equals("name", StringComparison.OrdinalIgnoreCase) &&
        !sort.Equals("type", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("Invalid sorting parameter");
        return;
    }

    Console.WriteLine("Remove empty lines? (true/false)");
    if (!bool.TryParse(Console.ReadLine(), out bool removeEmptyLines))
    {
        Console.WriteLine("Invalid input");
        return;
    }

    Console.WriteLine("Enter the author (optional):");
    string? author = Console.ReadLine();

    Console.WriteLine("Enter the response file name:");
    string? rspPath = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(rspPath))
    {
        Console.WriteLine("Invalid response file name");
        return;
    }

    var lines = new List<string>
{
    "bundle",
    "--output",
    $"\"{outputPath}\"",
    "--languages"
};

    lines.AddRange(languagesCheck.Select(lang => lang.TrimStart('.')));

    if (note)
        lines.Add("--note");

    lines.Add("--sort");
    lines.Add(sort);

    if (removeEmptyLines)
        lines.Add("--remove-empty-lines");

    if (!string.IsNullOrWhiteSpace(author))
    {
        lines.Add("--author");
        lines.Add(author);
    }

    try
    {
        File.WriteAllLines(rspPath, lines);
        Console.WriteLine("Response file created successfully");
    }
    catch (IOException)
    {
        Console.WriteLine("Could not write the response file");
    }
    catch (UnauthorizedAccessException)
    {
        Console.WriteLine("Access denied");
    }
});
rootCommand.Add(createRspCommand);
rootCommand.Parse(args).Invoke();

#region הפונקציה הקודמת
/*
     FileInfo? outputPath = parseResult.GetValue(output);
    string[]? selectedLanguages = parseResult.GetValue(languages);
    bool includeNote = parseResult.GetValue(note);
    string sortBy = parseResult.GetValue(sort) ?? "name";
    bool removeEmptyLines = parseResult.GetValue(remove_empty_lines);
    string? authorName = parseResult.GetValue(author);

    string currentDir = Directory.GetCurrentDirectory();
    IEnumerable<string> allFiles =
    Enumerable.Empty<string>();
    if (!sortBy.Equals("name", StringComparison.OrdinalIgnoreCase) &&
        !sortBy.Equals("type", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("שיטת המיון חייבת להיות name או type");
        return;
    }

    if (outputPath == null)
    {
        Console.WriteLine("לא ניתן לכתוב ללא נתיב");
        return;
    }
    if (selectedLanguages == null)
    {
        Console.WriteLine("חובה לציין את השפות הנדרשות");
        return;
    }
    else
    {
        var excludedFolders = new[] { "bin", "debug", "obj", ".git" };

        allFiles = Directory.EnumerateFiles(
            currentDir, "*", SearchOption.AllDirectories)
            .Where(file =>
                !file.Split(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar)
                     .Any(part => excludedFolders.Contains(
                         part, StringComparer.OrdinalIgnoreCase)));
        string outputFullPath;

        try
        {
            outputFullPath = Path.GetFullPath(outputPath.FullName);
        }
        catch (Exception ex) when (
            ex is ArgumentException ||
            ex is NotSupportedException ||
            ex is PathTooLongException)
        {
            Console.WriteLine($"נתיב הפלט אינו תקין: {ex.Message}");
            return;
        }
        allFiles = allFiles.Where(file =>
            !string.Equals(
                Path.GetFullPath(file),
                outputFullPath,
                StringComparison.OrdinalIgnoreCase));
        if (!selectedLanguages.Contains(
                "all", StringComparer.OrdinalIgnoreCase))
        {
            var languages = selectedLanguages
      .Select(lang => lang.StartsWith(".") ? lang : "." + lang)
      .ToHashSet(StringComparer.OrdinalIgnoreCase);

            allFiles = allFiles.Where(file =>
                languages.Contains(Path.GetExtension(file)));//הסבר על השורה הזאת
        }

        if (sortBy == "name")
        {
            allFiles = allFiles.OrderBy(
                file => Path.GetFileName(file),
                StringComparer.OrdinalIgnoreCase);
        }
        else if (sortBy == "type")
        {
            allFiles = allFiles
                .OrderBy(
                    file => Path.GetExtension(file),
                    StringComparer.OrdinalIgnoreCase)
                .ThenBy(
                    file => Path.GetFileName(file),
                    StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            using var writer = new StreamWriter(outputFullPath, false);

            // רישום שם היוצר בראש הקובץ
            if (!string.IsNullOrWhiteSpace(authorName))
            {
                writer.WriteLine($"// Author: {authorName}");
                writer.WriteLine();
            }

            foreach (string file in allFiles)
            {
                if (includeNote)
                {
                    string relativePath = Path.GetRelativePath(currentDir, file);

                    writer.WriteLine($"// Source: {relativePath}");
                }

                string content;

                if (removeEmptyLines)
                {
                    content = string.Join(
                        Environment.NewLine,
                        File.ReadAllLines(file)
                            .Where(line => !string.IsNullOrWhiteSpace(line)));
                }
                else
                {
                    content = File.ReadAllText(file);
                }
                writer.WriteLine(content);
                writer.WriteLine();
            }

            Console.WriteLine($"הקובץ נוצר בהצלחה: {outputFullPath}");
        }
        catch (Exception ex) when (
            ex is IOException ||
            ex is UnauthorizedAccessException ||
            ex is ArgumentException ||
            ex is NotSupportedException)
        {
            Console.WriteLine($"שגיאה ביצירת קובץ ה-bundle: {ex.Message}");
        }

    }
 */
/*
 createRspCommand.SetAction(_ =>
{
    Console.WriteLine("Enter the output file name:");
    string? outputPath = Console.ReadLine();
    Console.WriteLine("Enter the languages:");
    string? languages = Console.ReadLine();
    Console.WriteLine("Do you want to include the original file path? (true/false)");
    if (!bool.TryParse(Console.ReadLine(), out bool note))
    {
        Console.WriteLine("Invalid input");
        return;
    }
    Console.WriteLine("Enter 'name' or 'type' for sorting (default: name)");
    string? input = Console.ReadLine();
    string sort = string.IsNullOrWhiteSpace(input) ? "name" : input;
    Console.WriteLine("Enter if you want to remove empty lines");
    if (!bool.TryParse(Console.ReadLine(), out bool removeEmptyLines))
    {
        Console.WriteLine("Invalid input");
        return;
    }
    Console.WriteLine("Enter if you want to publish the author");
    string? author = Console.ReadLine();
    if (!string.IsNullOrWhiteSpace(outputPath))
    {
        try
        {
            Path.GetFullPath(outputPath);
        }
        catch (Exception ex) when (
            ex is ArgumentException ||
            ex is NotSupportedException ||
            ex is PathTooLongException)
        {
            Console.WriteLine("Invalid path");
        }

    }

    else
    {
        Console.WriteLine("Invalid path, you should try another path at next time");
        return;
    }
    ;
    if (string.IsNullOrWhiteSpace(languages))
    {
        Console.WriteLine("Languages required");
        return;
    }

    var languagesCheck = languages
        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Select(lang => lang.StartsWith(".") ? lang : "." + lang)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);


    if (note != false && note != true)
    {
        Console.WriteLine("Invalid input");
        return;
    }
    if (!(sort.Equals("type", StringComparison.OrdinalIgnoreCase) || sort.Equals("name", StringComparison.OrdinalIgnoreCase)))
    {
        Console.WriteLine("Invalid sorting parameter");
        return;
    }
    
    
    Console.WriteLine("Enter the response file name:");
    string? rspPath = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(rspPath))
    {
        Console.WriteLine("Invalid response file name");
        return;
    }

    var lines = new List<string>
{
    "bundle",
    "--output",
    outputPath!,
    "--languages"
};

    lines.AddRange(languagesCheck.Select(lang => lang.TrimStart('.')));

    if (note)
        lines.Add("--note");

    lines.Add("--sort");
    lines.Add(sort);

    if (removeEmptyLines)
        lines.Add("--remove-empty-lines");

    if (!string.IsNullOrWhiteSpace(author))
    {
        lines.Add("--author");
        lines.Add(author);
    }

    File.WriteAllLines(rspPath, lines);
    Console.WriteLine("Response file created successfully");
});

 */
#endregion

