namespace CoreAPI.Core.Models;

using CoreAPI.Core.Helpers;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;

public class BrigadeTypeConverter : TypeConverter
{
    // Redefining the CanConvertFrom method to specify that we can convert from a string
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) 
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    // Redefining the ConvertFrom method to implement the parsing logic
    public override object? ConvertFrom(ITypeDescriptorContext? context, System.Globalization.CultureInfo? culture, object? value)
    {
        if (value is string str && !string.IsNullOrWhiteSpace(str))
        {
            var parts = str.Split(',');
            if (parts.Length == 2 && uint.TryParse(parts[0], out uint id))
            {
                return new Brigade(id, parts[1].Trim()); 
            }
        }
        throw new ArgumentException($"Cannot convert \"{value}\" to Brigade.");
    }

    // Redefining the ConvertTo method to implement the Brigade conversion to a string
    public override object? ConvertTo(ITypeDescriptorContext? context, System.Globalization.CultureInfo? culture, object? value, Type destinationType)
        => destinationType == typeof(string) && value is Brigade brigade
            ? brigade.ToString()
            : base.ConvertTo(context, culture, value, destinationType);
}

/// <summary>
/// Represents a brigade with an ID and a name.
/// </summary>
[TypeConverter(typeof(BrigadeTypeConverter))]
public class Brigade
{
    //! ID
    private readonly uint _id;

    [Range(1, uint.MaxValue, ErrorMessage = "ID must be greater than zero.")]
    public uint Id => _id;

    //! NAME
    private string _name;

    [Required(ErrorMessage = "Name is required.")] // Name cannot be null or empty.
    public string Name
    {
        get => _name;
        set => ValidatorHelper.SetValueWithValidation(this, ref _name, nameof(Name), value); // Validation and assignment
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Brigade"/> class.
    /// </summary>
    /// <param name="id">The unique identifier of the brigade.</param>
    /// <param name="name">The name of the brigade.</param>
    public Brigade(uint id, string name)
    {
        _id = id;
        _name = name;

        ValidatorHelper.ValidateObject(this);
    }

    /// <summary>
    /// Returns a string representation of the brigade.
    /// </summary>
    /// <returns>A string containing the brigade's ID and name.</returns>
    public override string ToString()
    {
        return $"Brigade [Id: {Id}, Name: {Name}]";
    }
}