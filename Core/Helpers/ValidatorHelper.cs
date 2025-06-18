namespace CoreAPI.Core.Helpers;

using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.Reflection;

public static class ValidatorHelper
{
    // Method for object validation
    public static void ValidateObject<T>(T obj)
    {
        if (obj == null)
        {
            throw new ArgumentNullException(nameof(obj));
        }

        // Getting properties that belong only to the current class (not inherited)
        var properties = GetNonInheritedProperties(typeof(T));

        // Validating each property
        foreach (var property in properties)
        {
            // Getting the property value
            var value = property.GetValue(obj);

            // Validating the property value
            ValidateProperty(obj, property.Name, value);
        }
    }

    public static void SetValueWithValidation<T, K>(T obj, ref K field, string propertyName, K value)
    {
        ValidateProperty(obj, propertyName, value); // Validation
        field = value; // Assignment
    }

    // Method for validating a single property
    private static void ValidateProperty<T, K>(T obj, string propertyName, K value)
    {
        if (obj == null)
        {
            throw new ArgumentNullException(nameof(obj));
        }

        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(obj) { MemberName = propertyName };

        bool isValid = Validator.TryValidateProperty(value, context, validationResults);

        if (!isValid)
        {
            // Combining all error messages into one line
            string errorMessages = string.Join(Environment.NewLine, validationResults.Select(r => r.ErrorMessage));

            // Creating a general error message
            string errorMessage = $"Invalid value for '{propertyName}'. Errors:{Environment.NewLine}{errorMessages}";

            // Throwing an exception with a combined message
            throw new ValidationException(errorMessage);
        }
    }

    public static IEnumerable<PropertyInfo> GetNonInheritedProperties(Type type)
    {
        if (type == null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        // Getting all the properties of the current class
        var allProperties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        // Getting all the properties of the base class
        var baseProperties = type.BaseType?.GetProperties(BindingFlags.Public | BindingFlags.Instance) ?? Array.Empty<PropertyInfo>();

        // We exclude properties that are in the base class
        var nonInheritedProperties = allProperties.Where(p =>
        {
            // Exclude indexers (properties with parameters)
            bool isIndexer = p.GetIndexParameters().Any();
            
            return !isIndexer && !baseProperties.Any(bp => bp.Name == p.Name && bp.PropertyType == p.PropertyType);
        });

        return nonInheritedProperties;
    }
}