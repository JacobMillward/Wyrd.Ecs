using SDL3;

namespace Wyrd.Ecs.Input.Tests;

public class BindingTableTests
{
    [Fact]
    public void Bind_CalledTwice_IsAdditiveNotReplacing()
    {
        var table = new BindingTable<TestAction>();
        table.Bind(TestAction.Jump, SDL.Scancode.Space);
        table.Bind(TestAction.Jump, SDL.Scancode.Return);

        table.KeysFor(0, TestAction.Jump).Should().BeEquivalentTo([SDL.Scancode.Space, SDL.Scancode.Return]);
    }

    [Fact]
    public void Bind_MixingKeyAndMouseButton_BothApplyToTheSameAction()
    {
        var table = new BindingTable<TestAction>();
        table.Bind(TestAction.Jump, SDL.Scancode.Space);
        table.Bind(TestAction.Jump, MouseButton.Left);

        table.KeysFor(0, TestAction.Jump).Should().Contain(SDL.Scancode.Space);
        table.MouseButtonsFor(0, TestAction.Jump).Should().Contain(MouseButton.Left);
    }

    [Fact]
    public void Bind_WithNoProfileArgument_TargetsProfileZero()
    {
        var table = new BindingTable<TestAction>();
        table.Bind(TestAction.Jump, SDL.Scancode.Space);

        table.KeysFor(0, TestAction.Jump).Should().Contain(SDL.Scancode.Space);
        table.KeysFor(1, TestAction.Jump).Should().BeEmpty();
    }

    [Fact]
    public void Bind_WithAnExplicitProfile_DoesNotAffectProfileZero()
    {
        var table = new BindingTable<TestAction>();
        table.Bind(profile: 1, TestAction.Jump, SDL.Scancode.Space);

        table.KeysFor(1, TestAction.Jump).Should().Contain(SDL.Scancode.Space);
        table.KeysFor(0, TestAction.Jump).Should().BeEmpty();
    }

    [Fact]
    public void BindAxis2D_AfterBind_ForTheSameAction_Throws()
    {
        var table = new BindingTable<TestAction>();
        table.Bind(TestAction.Move, SDL.Scancode.Space);

        var act = () => table.BindAxis2D(TestAction.Move, SDL.Scancode.W, SDL.Scancode.S, SDL.Scancode.A, SDL.Scancode.D);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Move*digital*");
    }

    [Fact]
    public void Bind_AfterBindAxis2D_ForTheSameAction_Throws()
    {
        var table = new BindingTable<TestAction>();
        table.BindAxis2D(TestAction.Move, SDL.Scancode.W, SDL.Scancode.S, SDL.Scancode.A, SDL.Scancode.D);

        var act = () => table.Bind(TestAction.Move, SDL.Scancode.Space);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Move*axis*");
    }

    [Fact]
    public void Unbind_ClearsEveryBindingKindForThatActionAndProfile()
    {
        var table = new BindingTable<TestAction>();
        table.Bind(TestAction.Jump, SDL.Scancode.Space);
        table.Bind(TestAction.Jump, MouseButton.Left);

        table.Unbind(TestAction.Jump);

        table.KeysFor(0, TestAction.Jump).Should().BeEmpty();
        table.MouseButtonsFor(0, TestAction.Jump).Should().BeEmpty();
    }

    [Fact]
    public void Unbind_AfterClearingAnAction_AllowsRebindingItAsTheOtherKind()
    {
        var table = new BindingTable<TestAction>();
        table.Bind(TestAction.Move, SDL.Scancode.Space);

        table.Unbind(TestAction.Move);
        var act = () => table.BindAxis2D(TestAction.Move, SDL.Scancode.W, SDL.Scancode.S, SDL.Scancode.A, SDL.Scancode.D);

        act.Should().NotThrow();
    }

    [Fact]
    public void UnbindSingleKey_RemovesOnlyThatKey()
    {
        var table = new BindingTable<TestAction>();
        table.Bind(TestAction.Jump, SDL.Scancode.Space, SDL.Scancode.Return);

        table.Unbind(TestAction.Jump, SDL.Scancode.Space);

        table.KeysFor(0, TestAction.Jump).Should().BeEquivalentTo([SDL.Scancode.Return]);
    }

    [Fact]
    public void BoundActions_ReflectsEveryBindAndBindAxis2DCallAcrossProfiles()
    {
        var table = new BindingTable<TestAction>();
        table.Bind(TestAction.Jump, SDL.Scancode.Space);
        table.BindAxis2D(profile: 1, TestAction.Move, SDL.Scancode.W, SDL.Scancode.S, SDL.Scancode.A, SDL.Scancode.D);

        table.BoundActions().Should().BeEquivalentTo(
        [
            (0, TestAction.Jump, BindingTable<TestAction>.Kind.Digital),
            (1, TestAction.Move, BindingTable<TestAction>.Kind.Axis2D),
        ]);
    }

    [Fact]
    public void AssignDevice_CalledTwiceForTheSameProfile_AccumulatesBothDevices()
    {
        var table = new BindingTable<TestAction>();
        table.AssignDevice(0, 111u);
        table.AssignDevice(0, 222u);

        table.AssignedDevicesFor(0).Should().BeEquivalentTo([111u, 222u]);
    }

    [Fact]
    public void UnassignDevice_ClearsEveryDeviceForThatProfile()
    {
        var table = new BindingTable<TestAction>();
        table.AssignDevice(0, 111u);

        table.UnassignDevice(0);

        table.AssignedDevicesFor(0).Should().BeNull();
    }

    [Fact]
    public void UnassignDeviceById_RemovesOnlyThatDeviceFromWhicheverProfileHeldIt()
    {
        var table = new BindingTable<TestAction>();
        table.AssignDevice(0, 111u);
        table.AssignDevice(0, 222u);

        table.UnassignDeviceById(111u);

        table.AssignedDevicesFor(0).Should().BeEquivalentTo([222u]);
    }

    [Fact]
    public void UnassignedProfile_AssignedDevicesForReturnsNull_MeaningMergeEveryDevice() =>
        new BindingTable<TestAction>().AssignedDevicesFor(0).Should().BeNull();
}
