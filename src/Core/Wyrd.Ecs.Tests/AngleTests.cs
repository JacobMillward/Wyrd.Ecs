namespace Wyrd.Ecs.Tests;

public class AngleTests
{
    private const float Tolerance = 0.0001f;

    [Fact]
    public void Deg_And_Radians_RoundTripThroughRadiansConversion()
    {
        Angle.Deg(90f).Radians.Should().BeApproximately(MathF.PI / 2f, Tolerance);
    }

    [Fact]
    public void Rad_And_Degrees_RoundTripThroughDegreesConversion()
    {
        Angle.Rad(MathF.PI).Degrees.Should().BeApproximately(180f, Tolerance);
    }

    [Fact]
    public void Deg_ValueOver180Degrees_NormalizesIntoThePlusMinus180Range()
    {
        // 450 degrees is one full turn (360) plus 90.
        Angle.Deg(450f).Degrees.Should().BeApproximately(90f, Tolerance);
    }

    [Fact]
    public void Deg_NegativeValueBeyondNegative180_NormalizesIntoThePlusMinus180Range()
    {
        Angle.Deg(-450f).Degrees.Should().BeApproximately(-90f, Tolerance);
    }

    [Fact]
    public void Zero_HasNoRotation()
    {
        Angle.Zero.Radians.Should().Be(0f);
    }

    [Fact]
    public void AdditionOperator_SumsBothAngles()
    {
        (Angle.Deg(30f) + Angle.Deg(45f)).Degrees.Should().BeApproximately(75f, Tolerance);
    }

    [Fact]
    public void SubtractionOperator_SubtractsTheSecondAngle()
    {
        (Angle.Deg(30f) - Angle.Deg(45f)).Degrees.Should().BeApproximately(-15f, Tolerance);
    }

    [Fact]
    public void MultiplyOperator_ScalesByAFloat()
    {
        (Angle.Deg(30f) * 2f).Degrees.Should().BeApproximately(60f, Tolerance);
    }
}
