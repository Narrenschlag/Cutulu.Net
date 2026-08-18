namespace Cutulu.Core
{
    using System.Runtime.CompilerServices;
    using System.Collections.Generic;
    using System.Collections;
    using System;

    /// <summary>
    /// Provides deep comparison logic for any object, including collections, dictionaries, and graphs.
    /// Uses flags for configurable depth and behavior.
    /// </summary>
    public static class PropertyCompare
    {
        /// <summary>
        /// Flag-based comparison options.
        /// </summary>
        [Flags]
        public enum TYPE : byte
        {
            DEFAULT = 1,                     // Use basic Equals(), no reflection
            EXPENSIVE_BINARY = 2,            // Compare encoded buffers, expensive but very reliable
            PROPERTIES_AND_FIELDS = 4,       // Compare public fields and properties
            DICTIONARY_KEYS_DEEP = 8,        // Compare dictionary keys deeply
        }

        /// <summary>
        /// Performs a deep comparison between two objects using specified flags.
        /// </summary>
        public static bool IsEqualTo(this object obj, object other, TYPE settings = TYPE.DEFAULT)
        {
            return IsEqualTo(obj, other, settings, new(ReferencePairComparer.Instance));
        }

        /// <summary>
        /// Internal recursive logic with cycle detection and configurable behavior.
        /// </summary>
        private static bool IsEqualTo(object obj, object other, TYPE settings, HashSet<(object, object)> visited)
        {
            // Equals each other
            if (obj == other) return true;

            // Null comparison
            if (obj.IsNull() != other.IsNull()) return false;
            if (obj is null && other is null) return true;

            var type = obj.GetType();
            if (type != other.GetType()) return false;

            // Avoid infinite loops on circular references
            var pair = (obj, other);
            if (!visited.Add(pair)) return true;

            try
            {
                // Custom comparison
                if (obj is ICustomPropertyComparer custom)
                    return custom.CustomIsEqualTo(other);

                // Deep compare binary buffers
                if (settings.HasFlag(TYPE.EXPENSIVE_BINARY))
                    return obj.Encode().SequenceEquals(other.Encode());

                // Handle dictionaries
                if (obj is IDictionary dictA && other is IDictionary dictB)
                    return AreDictionariesEqual(dictA, dictB, settings, visited);

                // Handle collections (excluding string)
                if (obj is IEnumerable enumA && obj is not string)
                    return AreEnumerablesEqual(enumA, (IEnumerable)other, settings, visited);

                // Handle property and field-based comparison
                if (settings.HasFlag(TYPE.PROPERTIES_AND_FIELDS) &&
                    !type.IsPrimitive && !type.IsEnum && type != typeof(string))
                    return ArePropertiesAndFieldsEqual(obj, other, type, settings, visited);

                // Default fallback comparison
                return obj.Equals(other);
            }
            finally
            {
                visited.Remove(pair);
            }
        }

        /// <summary>
        /// Compares two dictionaries with optional deep key comparison.
        /// </summary>
        private static bool AreDictionariesEqual(IDictionary dictA, IDictionary dictB, TYPE settings, HashSet<(object, object)> visited)
        {
            if (dictA.Count != dictB.Count) return false;

            if (settings.HasFlag(TYPE.DICTIONARY_KEYS_DEEP))
            {
                foreach (DictionaryEntry entryA in dictA)
                {
                    bool found = false;

                    foreach (DictionaryEntry entryB in dictB)
                    {
                        if (IsEqualTo(entryA.Key, entryB.Key, settings, visited))
                        {
                            if (!IsEqualTo(entryA.Value, entryB.Value, settings, visited))
                                return false;

                            found = true;
                            break;
                        }
                    }

                    if (!found) return false;
                }

                return true;
            }

            // Shallow key comparison (fast path)
            foreach (DictionaryEntry entry in dictA)
            {
                if (!dictB.Contains(entry.Key)) return false;
                if (!IsEqualTo(entry.Value, dictB[entry.Key], settings, visited)) return false;
            }

            return true;
        }

        /// <summary>
        /// Compares two enumerables (e.g., arrays, lists) in order.
        /// </summary>
        private static bool AreEnumerablesEqual(IEnumerable a, IEnumerable b, TYPE settings, HashSet<(object, object)> visited)
        {
            var enumA = a.GetEnumerator();
            var enumB = b.GetEnumerator();

            while (true)
            {
                var hasA = enumA.MoveNext();
                var hasB = enumB.MoveNext();

                if (hasA != hasB) return false;
                if (!hasA) break;

                if (!IsEqualTo(enumA.Current, enumB.Current, settings, visited)) return false;
            }

            return true;
        }

        /// <summary>
        /// Recursively compares public properties and fields of complex objects.
        /// </summary>
        private static bool ArePropertiesAndFieldsEqual(object a, object b, Type type, TYPE settings, HashSet<(object, object)> visited)
        {
            object valA, valB;

            var infos = ParameterManager.Open(type).GetInfos();
            foreach (ref var info in infos)
            {
                if (info.HasIndexParameters()) continue; // skip indexers

                valA = info.GetValue(a);
                valB = info.GetValue(b);

                if (!IsEqualTo(valA, valB, settings, visited)) return false;
            }

            return true;
        }

        /// <summary>
        /// Prevents recursion by tracking object pairs based on reference identity.
        /// </summary>
        private sealed class ReferencePairComparer : IEqualityComparer<(object, object)>
        {
            public static readonly ReferencePairComparer Instance = new();

            public bool Equals((object, object) x, (object, object) y) =>
                ReferenceEquals(x.Item1, y.Item1) && ReferenceEquals(x.Item2, y.Item2);

            public int GetHashCode((object, object) obj) =>
                RuntimeHelpers.GetHashCode(obj.Item1) ^ RuntimeHelpers.GetHashCode(obj.Item2);
        }
    }

    public interface ICustomPropertyComparer
    {
        /// <summary>
        /// Do not reference this function in any way. It is exclusively for PropertyCompare.
        /// </summary>
        public bool CustomIsEqualTo(object value);
    }
}
