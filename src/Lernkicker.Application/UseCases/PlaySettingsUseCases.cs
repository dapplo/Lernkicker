using Lernkicker.Application.Ports;
using Lernkicker.Domain.Multiplayer;

namespace Lernkicker.Application.UseCases;

/// <summary>
/// What a parent decides about playing with others: which games a child may play, and with whom. Nothing is
/// unlocked until a parent does it.
/// </summary>
public sealed class PlaySettingsUseCases(IChildRepository children, IPlayRepository play, IPlayNotifier notifier, IUnitOfWork unitOfWork)
{
    public async Task<Result<PlaySettingsSummary>> GetAsync(Guid childId)
    {
        var child = await children.FindAsync(childId);
        if (child is null)
        {
            return AppError.NotFound(ErrorCodes.ChildNotFound);
        }

        var settings = await play.FindSettingsAsync(childId) ?? PlaySettings.Locked(child.FamilyId, child.Id);
        return Summary(settings);
    }

    public async Task<Result<PlaySettingsSummary>> UpdateAsync(
        Guid childId, IReadOnlyList<string> games, string reach, IReadOnlyList<Guid> families, IReadOnlyList<Guid> picked)
    {
        if (!PlaySettings.IsValid(games, reach, families, picked))
        {
            return AppError.Invalid(ErrorCodes.PlaySettingsInvalid);
        }

        var child = await children.FindAsync(childId);
        if (child is null)
        {
            return AppError.NotFound(ErrorCodes.ChildNotFound);
        }

        var settings = await play.FindSettingsAsync(childId);
        if (settings is null)
        {
            settings = new PlaySettings(child.FamilyId, child.Id);
            play.Add(settings);
        }

        settings.Update(games, reach, families, picked);
        await unitOfWork.SaveChangesAsync();
        notifier.LobbyChanged();
        return Summary(settings);
    }

    /// <summary>Whom the parent can pick for <see cref="PlayReach.Selected"/>.</summary>
    public async Task<Result<PlayPartners>> PartnersAsync(Guid familyId, Guid childId)
    {
        var child = await children.FindAsync(childId);
        if (child is null)
        {
            return AppError.NotFound(ErrorCodes.ChildNotFound);
        }

        var picked = (await play.FindSettingsAsync(childId))?.Children ?? [];
        // A child of another family is only listed when their parents opened them to this child
        var open = (await play.ListUnlockedAnyFamilyAsync())
            .Where(s => s.FamilyId != familyId && s.Allows(familyId, childId))
            .Select(s => s.ChildId);
        var players = await play.PlayersAnyFamilyAsync(open.Concat(picked).Distinct().ToList());
        return new PlayPartners(
            await play.OtherFamiliesAsync(familyId),
            players.Where(p => p.FamilyId != familyId).OrderBy(p => p.Name).ThenBy(p => p.FamilyName).ToList());
    }

    private static PlaySettingsSummary Summary(PlaySettings s) => new(s.Games, s.Reach, s.Families, s.Children);
}
