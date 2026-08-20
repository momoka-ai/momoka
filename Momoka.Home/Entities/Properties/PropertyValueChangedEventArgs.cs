using Momoka.Home;
namespace Momoka.Home.Entities.Properties;

public class PropertyValueChangedEventArgs : EventArgs
{
    public Property Property { get; }
    public object? NewValue { get; }

    public PropertyValueChangedEventArgs(Property property, object? newValue)
    {
        Property = property;
        NewValue = newValue;
    }
}
