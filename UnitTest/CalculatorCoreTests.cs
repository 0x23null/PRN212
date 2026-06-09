using DemoUnitTest_ConsoleApp;

namespace UnitTest;

public class CalculatorCoreTests
{
    [Theory]
    [InlineData(5, 3, 8)]
    [InlineData(10, -4, 6)]
    [InlineData(-5, -10, -15)]
    public void Add_ReturnsExpectedSum(int a, int b, int expected)
    {
        var calculator = new Calculator();

        var actual = calculator.Add(a, b);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(10, 4, 6)]
    [InlineData(5, 10, -5)]
    [InlineData(-5, -10, 5)]
    public void Subtract_ReturnsExpectedDifference(int a, int b, int expected)
    {
        var calculator = new Calculator();

        var actual = calculator.Subtract(a, b);

        Assert.Equal(expected, actual);
    }
}
