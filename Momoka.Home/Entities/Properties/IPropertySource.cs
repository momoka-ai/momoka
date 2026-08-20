namespace Momoka.Home.Entities.Properties;

/// <summary>
/// Capability of an object to hold per-instance properties (name → typed
/// value). Implementers expose the property list, the change event and the
/// single notification hook (events can only be raised from the declaring
/// type); all lookups, mutations and schema queries live in
/// <see cref="PropertySourceExtensions"/>.
/// </summary>
public interface IPropertySource
{
    List<Property> Properties { get; }

    event EventHandler<PropertyValueChangedEventArgs>? PropertyValueChanged;

    /// <summary>
    /// Raises <see cref="PropertyValueChanged"/>; called by the property-set
    /// extensions after a value changes. One expression-bodied line per
    /// implementer — the only per-class code this interface requires.
    /// </summary>
    void NotifyPropertyChanged(Property property, object? newValue);
}
