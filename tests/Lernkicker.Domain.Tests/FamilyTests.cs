using Lernkicker.Domain.Families;
using Lernkicker.Domain.Practice;

namespace Lernkicker.Domain.Tests;

public class FamilyTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(null, false, true)]
    [InlineData(4, false, true)]
    [InlineData(14, false, false)]
    [InlineData(null, true, true)]
    [InlineData(4, true, false)]
    public void Adult_has_no_grade_level(int? gradeLevel, bool isAdult, bool valid) =>
        Assert.Equal(valid, Child.IsValidGradeLevel(gradeLevel, isAdult));

    [Fact]
    public void Device_locks_after_five_wrong_pins_until_unlocked()
    {
        var device = new ChildDevice(Guid.NewGuid(), Guid.NewGuid(), "hash", "Phone", Now);

        for (var i = 0; i < ChildDevice.MaxWrongPins - 1; i++)
        {
            device.RecordWrongPin();
        }

        Assert.False(device.Locked);
        device.RecordWrongPin();
        Assert.True(device.Locked);

        device.Unlock();
        Assert.False(device.Locked);
        Assert.Equal(0, device.WrongPins);
    }

    [Fact]
    public void Correct_pin_resets_the_wrong_pin_count()
    {
        var device = new ChildDevice(Guid.NewGuid(), Guid.NewGuid(), "hash", "Phone", Now);
        device.RecordWrongPin();

        device.RecordLogin(Now);

        Assert.Equal(0, device.WrongPins);
        Assert.Equal(Now, device.LastUsedAt);
    }

    [Theory]
    [InlineData("1234", true)]
    [InlineData("0000", true)]
    [InlineData("123", false)]
    [InlineData("12345", false)]
    [InlineData("12a4", false)]
    [InlineData("١٢٣٤", false)]
    [InlineData(null, false)]
    public void Pin_is_four_ascii_digits(string? pin, bool valid) => Assert.Equal(valid, Pin.IsValid(pin));

    [Fact]
    public void Invitation_is_usable_once_and_until_it_expires()
    {
        var invitation = new Invitation("hash", null, " Familie Müller ", Now + Invitation.Lifetime);

        Assert.Equal("Familie Müller", invitation.NewFamilyName);
        Assert.True(invitation.IsUsable(Now));
        Assert.False(invitation.IsUsable(Now + Invitation.Lifetime + TimeSpan.FromSeconds(1)));

        invitation.Redeem(Now);
        Assert.False(invitation.IsUsable(Now));
    }

    [Fact]
    public void Device_label_is_trimmed_and_limited()
    {
        Assert.Equal("Phone", ChildDevice.CleanLabel("   ", "Phone"));
        Assert.Equal("Calvins Handy", ChildDevice.CleanLabel(" Calvins Handy ", "Phone"));
        Assert.Equal(ChildDevice.MaxLabelLength, ChildDevice.CleanLabel(new string('x', 300), "Phone").Length);
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData(49, 0)]
    [InlineData(50, 1)]
    [InlineData(70, 2)]
    [InlineData(90, 3)]
    [InlineData(100, 3)]
    public void Stars_by_best_percent(int? percent, int stars) => Assert.Equal(stars, Stars.For(percent));
}
