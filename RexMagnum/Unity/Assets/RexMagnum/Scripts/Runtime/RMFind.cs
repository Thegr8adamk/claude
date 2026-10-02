using System;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RexMagnum
{
    /// <summary>
    /// FindObjectsByType without the sort-mode argument. Newer Unity 6 versions mark the FindObjectsSortMode overloads
    /// obsolete (a Console warning per call site) and add overloads without it; older ones only have the sort-mode
    /// overloads. The newer overload is found once by reflection; if it is not there, the old one is used. Unsorted
    /// results either way (the old calls all passed FindObjectsSortMode.None).
    /// </summary>
    public static class RMFind
    {
        /// <summary>Every active loaded object of type T.</summary>
        public static T[] All<T>() where T : Object => Cache<T>.Find(false);

        /// <summary>Every loaded object of type T, inactive ones included.</summary>
        public static T[] AllWithInactive<T>() where T : Object => Cache<T>.Find(true);

        static class Cache<T> where T : Object
        {
            static bool looked;
            static MethodInfo noSort, noSortInactive;

            public static T[] Find(bool inactive)
            {
                if (!looked) Look();
                if (inactive)
                {
                    if (noSortInactive != null) return (T[])noSortInactive.Invoke(null, new object[] { FindObjectsInactive.Include });
                    return Old(true);
                }
                if (noSort != null) return (T[])noSort.Invoke(null, null);
                return Old(false);
            }

            static void Look()
            {
                looked = true;
                foreach (var m in typeof(Object).GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (m.Name != "FindObjectsByType" || !m.IsGenericMethodDefinition) continue;
                    var ps = m.GetParameters();
                    try
                    {
                        if (ps.Length == 0) noSort = m.MakeGenericMethod(typeof(T));
                        else if (ps.Length == 1 && ps[0].ParameterType == typeof(FindObjectsInactive))
                            noSortInactive = m.MakeGenericMethod(typeof(T));
                    }
                    catch (ArgumentException) { }
                }
            }

#pragma warning disable 618   // the old overload: only used where the new one does not exist
            static T[] Old(bool inactive) => inactive
                ? Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                : Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#pragma warning restore 618
        }
    }
}
