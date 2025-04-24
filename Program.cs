using System;
using System.IO;

public static class Program
{
    static string inputFilePath = @"";  // Changed to relative path
    static string outputFilePath = @"";
    static string generatlPrompt = "Now you are an expert MATLAB programer.\n"
                         + "Given a problem, answer it using matlab.\n"
                         + "Your anwer should be only a matlab file, dont include ```matlab nor the name of the file.\n\n"
                         + "Problem: ";

    private static DateTime _lastReadTime = DateTime.MinValue;

    public static void Main(string[] args)
    {        

        if (!File.Exists("paths.txt")) throw new Exception("File 'paths.txt' does not exist");
        string[] paths = File.ReadAllText("paths.txt").Split('\n', 2);
        inputFilePath = paths[0];
        outputFilePath = paths[1];

        // Get the full path to the directory containing the input file
        string directoryPath = Path.GetDirectoryName(Path.GetFullPath(inputFilePath));
        
        // If no directory specified, use current directory
        if (string.IsNullOrEmpty(directoryPath))
            directoryPath = Directory.GetCurrentDirectory();

        using var watcher = new FileSystemWatcher(directoryPath)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
            Filter = Path.GetFileName(inputFilePath),
            EnableRaisingEvents = true
        };

        watcher.Changed += OnChanged;
        watcher.Created += OnChanged;  // Handle file creation
        watcher.Renamed += OnChanged;  // Handle file rename

        // Keep the application running
        Console.WriteLine("Running until ESC is pressed...");
        while (Console.ReadKey().Key != ConsoleKey.Escape) 
        {}
    }

    private static void OnChanged(object sender, FileSystemEventArgs e)
    {
        // Debounce multiple events - only process if it's been at least 500ms since last read
        if ((DateTime.Now - _lastReadTime).TotalMilliseconds < 500)
            return;

        _lastReadTime = DateTime.Now;

        Console.WriteLine("Changed");

        try
        {
            // Small delay to ensure file is completely written
            System.Threading.Thread.Sleep(100);

            if (!File.Exists(inputFilePath))
                return;

            string text = File.ReadAllText(inputFilePath);
            string prompt = generatlPrompt + text;

            // Optionally write to output file
            Task<string> request = new DeepSeekClient().GetDeepSeekResponseAsync(prompt);
            request.Wait();

            File.WriteAllText(outputFilePath, request.Result);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
}
