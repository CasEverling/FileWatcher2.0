using System;
using System.IO;
using System.Threading.Tasks;

public static class Program
{
    static string inputFilePath = @"";
    static string outputFilePath = @"";
    static string generatlPrompt = "Now you are an expert MATLAB programer.\n"
                         + "Given a problem, answer it using matlab.\n"
                         + "Your anwer should be only a matlab file, dont include ```matlab nor the name of the file.\n\n"
                         + "Problem: ";

    private static DateTime _lastReadTime = DateTime.MinValue;

    public static void Main(string[] args)
    {        
        // Check if paths.txt exists
        if (!File.Exists("paths.txt")) 
            throw new Exception("File 'paths.txt' does not exist");
        
        // Read paths from file
        string[] paths = File.ReadAllText("paths.txt").Split('\n', 2);
        if (paths.Length < 2)
            throw new Exception("paths.txt should contain two lines: input path and output path");
        
        inputFilePath = paths[0].Trim();
        outputFilePath = paths[1].Trim();
        
        // Validate input path
        string fullInputPath = Path.GetFullPath(inputFilePath);
        string directoryPath = Path.GetDirectoryName(fullInputPath);
        
        // Check if directory exists
        if (!Directory.Exists(directoryPath))
        {
            Console.WriteLine($"Directory does not exist: {directoryPath}");
            Console.WriteLine("Creating directory...");
            try
            {
                Directory.CreateDirectory(directoryPath);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to create directory: {ex.Message}");
            }
        }
        
        // Check if file exists, create empty file if needed
        if (!File.Exists(fullInputPath))
        {
            Console.WriteLine($"Input file does not exist: {fullInputPath}");
            Console.WriteLine("Creating empty input file...");
            try
            {
                File.WriteAllText(fullInputPath, "");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to create input file: {ex.Message}");
            }
        }
        
        // Validate output path
        string fullOutputPath = Path.GetFullPath(outputFilePath);
        string outputDirPath = Path.GetDirectoryName(fullOutputPath);
        
        // Check if output directory exists
        if (!Directory.Exists(outputDirPath))
        {
            Console.WriteLine($"Output directory does not exist: {outputDirPath}");
            Console.WriteLine("Creating output directory...");
            try
            {
                Directory.CreateDirectory(outputDirPath);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to create output directory: {ex.Message}");
            }
        }
        
        // Set up file watcher
        using var watcher = new FileSystemWatcher(directoryPath)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | 
                         NotifyFilters.CreationTime | NotifyFilters.Size,
            Filter = Path.GetFileName(inputFilePath),
            EnableRaisingEvents = true
        };

        Console.WriteLine($"Watching for changes to: {fullInputPath}");
        Console.WriteLine($"Output will be written to: {fullOutputPath}");

        watcher.Changed += OnChanged;
        watcher.Created += OnChanged;
        watcher.Renamed += OnChanged;

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
                {
                    Console.WriteLine($"Warning: Input file no longer exists: {inputFilePath}");
                    return;
                }
                    
                // Try to open the file with FileShare.ReadWrite to handle locks
                using (FileStream fileStream = new FileStream(inputFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (StreamReader reader = new StreamReader(fileStream))
                {
                    string text = reader.ReadToEnd();
                    string prompt = generatlPrompt + text;
                    
                    Console.WriteLine("Sending request to DeepSeek...");
                    Task<string> request = new DeepSeekClient().GetDeepSeekResponseAsync(prompt);
                    request.Wait();
                    
                    try
                    {
                        File.WriteAllText(outputFilePath, request.Result);
                        Console.WriteLine($"Successfully wrote response to: {outputFilePath}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error writing to output file: {ex.Message}");
                    }
                    
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

// Assuming DeepSeekClient is defined elsewhere like this:
// public class DeepSeekClient
// {
//     public Task<string> GetDeepSeekResponseAsync(string prompt)
//     {
//         // Implementation here
//     }
// }
