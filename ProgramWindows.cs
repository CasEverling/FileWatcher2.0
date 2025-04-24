using System;
using System.IO;
using System.Threading.Tasks;

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
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | 
                         NotifyFilters.CreationTime | NotifyFilters.Size,
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

        Console.WriteLine($"Change detected: {e.ChangeType} - {e.FullPath}");

        // Use a retry approach with exponential backoff
        int retryCount = 0;
        const int maxRetries = 5;
        
        while (retryCount < maxRetries)
        {
            try
            {
                // More substantial delay for Windows
                System.Threading.Thread.Sleep(100 * (retryCount + 1));
                
                if (!File.Exists(inputFilePath))
                    return;
                    
                // Try to open the file with FileShare.ReadWrite to handle locks
                using (FileStream fileStream = new FileStream(inputFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (StreamReader reader = new StreamReader(fileStream))
                {
                    string text = reader.ReadToEnd();
                    string prompt = generatlPrompt + text;
                    
                    Task<string> request = new DeepSeekClient().GetDeepSeekResponseAsync(prompt);
                    request.Wait();
                    
                    File.WriteAllText(outputFilePath, request.Result);
                    Console.WriteLine("Successfully processed file change");
                    return; // Success, exit the retry loop
                }
            }
            catch (IOException ex)
            {
                retryCount++;
                Console.WriteLine($"Attempt {retryCount}: File still locked. Retrying... ({ex.Message})");
                
                if (retryCount >= maxRetries)
                    Console.WriteLine("Max retries reached. Could not process file.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing file: {ex.Message}");
                return; // Don't retry on non-IO exceptions
            }
        }
    }
}
