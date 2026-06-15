using Blackbird.Applications.Sdk.Common.Files;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using System.Text;

namespace Tests.Payload.Base;

public class FileManager : IFileManagementClient
{
    private readonly string _inputFolder;
    private readonly string _outputFolder;

    public FileManager()
    {
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var projectDirectory = Directory.GetParent(baseDirectory)?.Parent?.Parent?.Parent?.FullName
                               ?? throw new Exception("Could not resolve project directory.");

        var testFilesPath = Path.Combine(projectDirectory, "TestFiles");
        _inputFolder = Path.Combine(testFilesPath, "Input");
        _outputFolder = Path.Combine(testFilesPath, "Output");

        Directory.CreateDirectory(_inputFolder);
        Directory.CreateDirectory(_outputFolder);
    }

    public Task<Stream> DownloadAsync(FileReference reference)
    {
        var path = Path.Combine(_inputFolder, reference.Name);
        Assert.IsTrue(File.Exists(path), $"Input file not found: {path}");
        Stream stream = new MemoryStream(File.ReadAllBytes(path));
        return Task.FromResult(stream);
    }

    public string ReadOutputAsString(FileReference reference)
    {
        var path = Path.Combine(_outputFolder, reference.Name);
        Assert.IsTrue(File.Exists(path), $"Output file not found: {path}");
        return File.ReadAllText(path, Encoding.UTF8);
    }

    public Task<FileReference> UploadAsync(Stream stream, string contentType, string fileName)
    {
        var path = Path.Combine(_outputFolder, fileName);
        new FileInfo(path).Directory?.Create();
        using var fileStream = File.Create(path);
        stream.CopyTo(fileStream);
        return Task.FromResult(new FileReference { Name = fileName, ContentType = contentType });
    }
}
