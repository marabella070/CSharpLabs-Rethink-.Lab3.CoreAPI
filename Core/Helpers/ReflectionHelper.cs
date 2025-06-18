using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public static class ReflectionHelper
{
    public static List<string> GetMethodsInfo(Type type)
    {
        var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
        if (!methods.Any()) return new List<string> { "There are no methods." };

        return methods.Select(method =>
        {
            string accessModifier = GetAccessModifier(method);
            string staticModifier = method.IsStatic ? "static " : "";
            return $"{accessModifier} {staticModifier}{method.ReturnType.Name} {method.Name}({string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))});";
        }).ToList();
    }

    public static List<string> GetFieldsInfo(Type type)
    {
        var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        if (!fields.Any()) return new List<string> { "There are no fields." };

        return fields.Select(field =>
        {
            string accessModifier = GetAccessModifier(field);
            string staticModifier = field.IsStatic ? "static " : "";
            return $"{accessModifier} {staticModifier}{field.FieldType.Name} {field.Name};";
        }).ToList();
    }

    public static List<string> GetPropertiesInfo(Type type)
    {
        var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
        if (!properties.Any()) return new List<string> { "There are no properties." };

        return properties.Select(property =>
        {
            string getModifier = GetAccessModifier(property.GetMethod);
            string setModifier = GetAccessModifier(property.SetMethod);
            string staticModifier = ((property.GetMethod?.IsStatic ?? false) || (property.SetMethod?.IsStatic ?? false)) ? "static " : "";
            return $"{getModifier} {staticModifier}{property.PropertyType.Name} {property.Name} {{ {getModifier} get; {setModifier} set; }}";
        }).ToList();
    }

    public static List<string> GetIndexersInfo(Type type)
    {
        var indexers = type.GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly)
                           .Where(p => p.GetIndexParameters().Any());
        if (!indexers.Any()) return new List<string> { "There are no indexers." };

        return indexers.Select(indexer =>
        {
            string getModifier = GetAccessModifier(indexer.GetMethod);
            string setModifier = GetAccessModifier(indexer.SetMethod);
            string staticModifier = ((indexer.GetMethod?.IsStatic ?? false) || (indexer.SetMethod?.IsStatic ?? false)) ? "static " : "";
            return $"{getModifier} {staticModifier}{indexer.PropertyType.Name} this[{string.Join(", ", indexer.GetIndexParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))}] {{ {getModifier} get; {setModifier} set; }}";
        }).ToList();
    }

    private static string GetAccessModifier(MemberInfo? member)
    {
        if (member is MethodBase method)
        {
            if (method.IsPublic) return "public";
            if (method.IsPrivate) return "private";
            if (method.IsFamily) return "protected";
            if (method.IsAssembly) return "internal";
            if (method.IsFamilyOrAssembly) return "protected internal";
        }
        else if (member is FieldInfo field)
        {
            if (field.IsPublic) return "public";
            if (field.IsPrivate) return "private";
            if (field.IsFamily) return "protected";
            if (field.IsAssembly) return "internal";
            if (field.IsFamilyOrAssembly) return "protected internal";
        }

        return "unknown";
    }
}
