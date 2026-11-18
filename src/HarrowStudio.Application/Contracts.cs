using HarrowStudio.Domain;

namespace HarrowStudio.Application;

public interface IProfileRepository
{
    Task<ControllerProfile> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(ControllerProfile profile, CancellationToken cancellationToken = default);
}

public interface IControllerInput
{
    ControllerState Read();
}

public sealed record AppRelease(Version Version, string TagName, Uri PageUri, Uri InstallerUri, string ReleaseNotes);

public interface IUpdateService
{
    Task<AppRelease?> CheckForUpdateAsync(Version currentVersion, CancellationToken cancellationToken = default);
    Task DownloadInstallerAsync(AppRelease release, string destinationPath, CancellationToken cancellationToken = default);
}

public sealed class ProfileService(IProfileRepository repository)
{
    public Task<ControllerProfile> LoadAsync(CancellationToken cancellationToken = default) => repository.LoadAsync(cancellationToken);
    public Task SaveAsync(ControllerProfile profile, CancellationToken cancellationToken = default) => repository.SaveAsync(profile, cancellationToken);
}
