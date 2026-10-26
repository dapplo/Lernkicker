using Lernkicker.Application.Ports;
using Lernkicker.Domain.Families;

namespace Lernkicker.Application.UseCases;

/// <summary>The child's phone: pairing with a parent's code and logging in with the PIN.</summary>
public sealed class ChildDeviceUseCases(IChildRepository children, IPinHasher pinHasher, IUnitOfWork unitOfWork, TimeProvider time)
{
    public const string DefaultLabel = "Phone";

    /// <summary>Pairs the device and returns the token the web adapter keeps in the device cookie.</summary>
    public async Task<Result<PairedDevice>> PairAsync(string code, string? label)
    {
        var pairing = await children.FindPairingCodeAnyFamilyAsync(Secrets.ShortCodeHash(code));
        var child = pairing is null ? null : await children.FindAnyFamilyAsync(pairing.ChildId);
        if (pairing is null || child is null || !pairing.IsValid(time.GetUtcNow()))
        {
            return AppError.Invalid(ErrorCodes.PairingCodeInvalid);
        }

        var token = Secrets.NewToken();
        children.Add(new ChildDevice(pairing.FamilyId, pairing.ChildId, Secrets.Hash(token), ChildDevice.CleanLabel(label, DefaultLabel), time.GetUtcNow()));
        children.Remove(pairing);
        await unitOfWork.SaveChangesAsync();
        return new PairedDevice(token, child.Name, child.Theme);
    }

    public async Task<DeviceStatus> StatusAsync(string? token)
    {
        var device = await DeviceAsync(token);
        var child = device is null ? null : await children.FindAnyFamilyAsync(device.ChildId);
        return device is null || child is null
            ? new DeviceStatus(false, null, false)
            : new DeviceStatus(true, child.Name, device.Locked, child.Theme);
    }

    public async Task<Result<ChildSession>> LoginAsync(string? token, string pin)
    {
        var device = await DeviceAsync(token);
        var child = device is null ? null : await children.FindAnyFamilyAsync(device.ChildId);
        if (device is null || child is null)
        {
            return new AppError(ErrorKind.Unauthorized, ErrorCodes.DeviceNotPaired);
        }

        if (device.Locked)
        {
            return new AppError(ErrorKind.Locked, ErrorCodes.DeviceLocked);
        }

        if (child.PinHash is null || !Pin.IsValid(pin) || !pinHasher.Verify(child.PinHash, pin))
        {
            device.RecordWrongPin();
            await unitOfWork.SaveChangesAsync();
            return device.Locked
                ? new AppError(ErrorKind.Locked, ErrorCodes.DeviceLocked)
                : new AppError(ErrorKind.Unauthorized, ErrorCodes.PinWrong);
        }

        device.RecordLogin(time.GetUtcNow());
        await unitOfWork.SaveChangesAsync();
        return new ChildSession(child.FamilyId, child.Id, device.Id, child.Name);
    }

    /// <summary>Forgets the pairing from the device itself, e.g. when it was paired for the wrong child.</summary>
    public async Task UnpairAsync(string? token)
    {
        var device = await DeviceAsync(token);
        if (device is null)
        {
            return;
        }

        children.Remove(device);
        await unitOfWork.SaveChangesAsync();
    }

    public Task<bool> IsDeviceActiveAsync(Guid deviceId) => children.IsDeviceActiveAnyFamilyAsync(deviceId);

    /// <summary>False once the profile is deleted or no longer the parent's own.</summary>
    public async Task<bool> IsLearnerActiveAsync(Guid childId, Guid parentId) =>
        (await children.FindAnyFamilyAsync(childId))?.LearnerParentId == parentId;

    public async Task<DeviceStatus> LearnerStatusAsync(Guid childId)
    {
        var child = await children.FindAnyFamilyAsync(childId);
        return child is null ? new DeviceStatus(false, null, false) : new DeviceStatus(true, child.Name, false, child.Theme, Learner: true);
    }

    private async Task<ChildDevice?> DeviceAsync(string? token) =>
        string.IsNullOrEmpty(token) ? null : await children.FindDeviceByTokenAnyFamilyAsync(Secrets.Hash(token));
}
