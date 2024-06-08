using System.Collections.Generic;

namespace Generatr._Depreciated;

internal static class Constants
{

    internal static class InvalidChars
    {
        //internal static readonly HashSet<char> AccessModifiers = [];
        internal static readonly HashSet<char> VariableNames = [' ', '\'', '<', '>'];
        internal static readonly HashSet<char> NamespaceNames = [' ', '\'', '<', '>'];
        internal static readonly HashSet<char> FieldNames = [' ', '\'', '<', '>'];
        internal static readonly HashSet<char> PropertyNames = [' ', '\'', '<', '>'];
        internal static readonly HashSet<char> ClassNames = [' ', '\'', '<', '>'];
        internal static readonly HashSet<char> TypeNames = [' ', '\''];
        internal static readonly HashSet<char> NoInvalidChars = []; // Often Used when the only way to create name builders is internally with constants
    }


    //private void DefaultNameInvalidAssertion(string name, HashSet<char> invalidChars)
    //{
    //    if (name.Length == 0 || char.IsNumber(name[0]) || name.Any(invalidChars.Contains) || AdditionalNameAssertions(name))
    //        throw new ArgumentOutOfRangeException(nameof(name), name, $"Name: \"{name}\" contains invalid chars.");
    //}
}