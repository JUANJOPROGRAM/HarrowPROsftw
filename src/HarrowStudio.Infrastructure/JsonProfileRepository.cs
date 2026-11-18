using System.Text.Json;
using HarrowStudio.Application;
using HarrowStudio.Domain;

namespace HarrowStudio.Infrastructure;

public sealed class JsonProfileRepository : IProfileRepository
{
    private readonly string _profilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HarrowStudio", "profiles", "default.json");

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public async Task<ControllerProfile> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_profilePath)) return ControllerProfile.CreateDefault();
        try
        {
            await using var stream = File.OpenRead(_profilePath);
            return await JsonSerializer.DeserializeAsync<ControllerProfile>(stream, Options, cancellationToken)
                ?? ControllerProfile.CreateDefault();
        }
        catch (JsonException)
        {
            try
            {
                var backupPath = $"{_profilePath}.corrupt-{Guid.NewGuid():N}.json";
                File.Copy(_profilePath, backupPath, overwrite: false);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }

            return ControllerProfile.CreateDefault();
        }
    }

    public async Task SaveAsync(ControllerProfile profile, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_profilePath)!);
        var temporaryPath = $"{_profilePath}.{Environment.ProcessId}.tmp";
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, profile, Options, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            File.Move(temporaryPath, _profilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}
