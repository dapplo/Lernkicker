using System.ComponentModel;
using Lernkicker.Application;
using Lernkicker.Application.UseCases;
using Lernkicker.Contracts;
using Lernkicker.Web.Endpoints;
using Lernkicker.Web.Security;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;

namespace Lernkicker.Web.Mcp;

/// <summary>
/// A child or adult learner of the family, as the assistant sees it. <c>ParentLogin</c>: an adult profile a parent
/// learns with through their own login (no PIN or phone needed).
/// </summary>
public sealed record ChildInfo(Guid Id, string Name, int? GradeLevel, bool IsAdult, string Theme, bool HasPin, int Devices, bool ParentLogin);

/// <summary>A one-time code to pair a phone with a profile: open <c>Link</c> on the phone or show it as a QR code.</summary>
public sealed record PairingLink(string Code, string Link, DateTimeOffset ExpiresAt);

[McpServerToolType]
public sealed class FamilyTools(ChildrenUseCases children, IHttpContextAccessor http)
{
    [McpServerTool(Name = "list_children", Title = "List children", ReadOnly = true, Idempotent = true)]
    [Description("The family's children and adult learner profiles with their ids, school year (gradeLevel), look (theme), whether a PIN is set, the number of paired phones and whether a parent learns with it through their own login. Use the id for assignments, results and the calendar.")]
    public async Task<IReadOnlyList<ChildInfo>> ListChildren() =>
        (await children.ListAsync()).Select(ToInfo).ToList();

    [McpServerTool(Name = "create_child", Title = "Add a child or adult learner")]
    [Authorize(Policy = Policies.ApiKeyWrite)]
    [Description("Adds a learner profile to the family: a child with a school year, or an adult (isAdult, no school year, no school marks), e.g. a parent who learns a language. A parent can then learn with an adult profile through their own login (\"Das bin ich: hier lernen\" on the profile's page), without PIN or phone. A child's phone is paired with create_pairing_link and needs a PIN.")]
    public async Task<ChildInfo> CreateChild(
        [Description("First name, at most 50 characters.")] string name,
        [Description("School year 1-13; omit for adults.")] int? gradeLevel = null,
        [Description("true for an adult learner (then without gradeLevel).")] bool isAdult = false,
        [Description("Four digits for logging in on a paired phone; omit to set it later.")] string? pin = null,
        [Description("The look of the app: football (default), space or unicorn.")] string? theme = null) =>
        ToInfo((await children.CreateAsync(http.HttpContext!.User.RequiredFamily(), name, gradeLevel, pin, isAdult, theme)).Value());

    [McpServerTool(Name = "update_child", Title = "Change a child's profile", Idempotent = true)]
    [Authorize(Policy = Policies.ApiKeyWrite)]
    [Description("Changes a profile's name, school year, adult flag or look. Omitted values stay as they are; to make a child an adult, pass isAdult true (the school year is dropped).")]
    public async Task<ChildInfo> UpdateChild(
        [Description("The child's id (list_children).")] Guid childId,
        [Description("New first name.")] string? name = null,
        [Description("New school year 1-13.")] int? gradeLevel = null,
        [Description("true for an adult learner, false for a child.")] bool? isAdult = null,
        [Description("football, space or unicorn.")] string? theme = null)
    {
        var current = await FindAsync(childId);
        var adult = isAdult ?? current.IsAdult;
        var grade = adult ? null : gradeLevel ?? current.GradeLevel;
        (await children.UpdateAsync(childId, name ?? current.Name, grade, adult, theme)).Check();
        return ToInfo(await FindAsync(childId));
    }

    [McpServerTool(Name = "delete_child", Title = "Remove a child", Destructive = true, Idempotent = true)]
    [Authorize(Policy = Policies.ApiKeyWrite)]
    [Description("Removes a profile for good, with its paired phones, assignments, results and calendar. Only do this when the parent asked for it.")]
    public async Task<string> DeleteChild([Description("The child's id (list_children).")] Guid childId)
    {
        (await children.DeleteAsync(childId)).Check();
        return "deleted";
    }

    [McpServerTool(Name = "set_child_pin", Title = "Set a child's PIN", Idempotent = true)]
    [Authorize(Policy = Policies.ApiKeyWrite)]
    [Description("Sets the four-digit PIN the child types on a paired phone.")]
    public async Task<string> SetChildPin(
        [Description("The child's id.")] Guid childId,
        [Description("Exactly four digits.")] string pin)
    {
        (await children.SetPinAsync(childId, pin)).Check();
        return "pin set";
    }

    [McpServerTool(Name = "create_pairing_link", Title = "Pair a phone with a child")]
    [Authorize(Policy = Policies.ApiKeyWrite)]
    [Description("A one-time link (valid for a short time) that pairs a phone with the profile: open it on the phone or show it as a QR code. The profile needs a PIN to log in on the phone (set_child_pin).")]
    public async Task<PairingLink> CreatePairingLink([Description("The child's id.")] Guid childId)
    {
        var pairing = (await children.CreatePairingCodeAsync(childId)).Value();
        var request = http.HttpContext!.Request;
        return new PairingLink(pairing.Code, $"{request.Scheme}://{request.Host}/pair#{pairing.Code}", pairing.ExpiresAt);
    }

    [McpServerTool(Name = "list_devices", Title = "A child's phones", ReadOnly = true, Idempotent = true)]
    [Description("The phones paired with a profile: when paired, last used and whether locked after too many wrong PINs.")]
    public async Task<IReadOnlyList<DeviceOverview>> ListDevices([Description("The child's id.")] Guid childId)
    {
        await FindAsync(childId);
        return (await children.ListDevicesAsync(childId)).Select(d => d.ToDto()).ToList();
    }

    [McpServerTool(Name = "unlock_device", Title = "Unlock a child's phone", Idempotent = true)]
    [Authorize(Policy = Policies.ApiKeyWrite)]
    [Description("Unlocks a phone that was locked after too many wrong PINs.")]
    public async Task<string> UnlockDevice(
        [Description("The child's id.")] Guid childId,
        [Description("The device id (list_devices).")] Guid deviceId)
    {
        (await children.UnlockDeviceAsync(childId, deviceId)).Check();
        return "unlocked";
    }

    [McpServerTool(Name = "remove_device", Title = "Unpair a child's phone", Destructive = true, Idempotent = true)]
    [Authorize(Policy = Policies.ApiKeyWrite)]
    [Description("Unpairs a phone, e.g. a lost one; it is logged out at once.")]
    public async Task<string> RemoveDevice(
        [Description("The child's id.")] Guid childId,
        [Description("The device id (list_devices).")] Guid deviceId)
    {
        (await children.RemoveDeviceAsync(childId, deviceId)).Check();
        return "removed";
    }

    [McpServerTool(Name = "get_authoring_guide", Title = "How to write quizzes and lessons", ReadOnly = true, Idempotent = true)]
    [Description("The guide for writing quiz/v1 and lesson/v1 files, followed by both JSON schemas. Read it before writing or changing a quiz or lesson.")]
    public static string GetAuthoringGuide([Description("Language of the guide: en or de.")] string language = "en") =>
        FormatEndpoints.Prompt(language == "de" ? "de" : "en");

    private async Task<ChildSummary> FindAsync(Guid childId) =>
        (await children.ListAsync()).FirstOrDefault(c => c.Id == childId) ?? throw McpResults.Failure(AppError.NotFound(ErrorCodes.ChildNotFound));

    private static ChildInfo ToInfo(ChildSummary c) =>
        new(c.Id, c.Name, c.GradeLevel, c.IsAdult, c.Theme, c.HasPin, c.Devices, c.LearnerParentId is not null);
}
