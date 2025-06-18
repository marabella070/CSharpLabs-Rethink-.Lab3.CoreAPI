using System.Data.Common;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;

using CoreAPI.Core.Models;
using CoreAPI.Core.Helpers;
using CoreAPI.Core.Randomizers;
using System.Reflection;


Workshop emptyWorkshop = Workshop.CreateEmpty();
Console.WriteLine(emptyWorkshop);

Console.WriteLine();
Console.WriteLine();
Console.WriteLine();

const int workshopsNumber = 3;
var workshops = WorkshopRandomizer.GenerateMultiple(workshopsNumber);

foreach (var workshop in workshops)
{
    Console.WriteLine(workshop);
    Console.WriteLine();
}

/*
Type type = typeof(ValidatorHelper);

Console.WriteLine($"Type: \"{type}\"\n");

//! FIELDS OUTPUT
List<string> typeFields = ReflectionHelper.GetFieldsInfo(type);

Console.WriteLine("Fields:");
foreach (var field in typeFields)
{
    Console.WriteLine(field);

}
Console.WriteLine();

//! PROPERTIES OUTPUT
List<string> typeProperties = ReflectionHelper.GetPropertiesInfo(type);

Console.WriteLine("Properties:");
foreach (var property in typeProperties)
{
    Console.WriteLine(property);

}
Console.WriteLine();

//! INDEXERS OUTPUT
List<string> typeIndexers = ReflectionHelper.GetIndexersInfo(type);

Console.WriteLine("Indexers:");
foreach (var indexer in typeIndexers)
{
    Console.WriteLine(indexer);

}
Console.WriteLine();

//! METHODS OUTPUT
List<string> typeMethods = ReflectionHelper.GetMethodsInfo(type);

Console.WriteLine("Methods:");
foreach (var method in typeMethods)
{
    Console.WriteLine(method);

}
Console.WriteLine();
*/


//                                                                                         __         ____     
//                                                   __    __  ____ ___  ____ __________ _/ /_  ___  / / /___ _
//                                                __/ /___/ /_/ __ `__ \/ __ `/ ___/ __ `/ __ \/ _ \/ / / __ `/
//                                               /_  __/_  __/ / / / / / /_/ / /  / /_/ / /_/ /  __/ / / /_/ / 
//                                                /_/   /_/ /_/ /_/ /_/\__,_/_/   \__,_/_.___/\___/_/_/\__,_/  