﻿// Based on code made by MarC0 / ManlyMarco
// Copyright 2018 GNU General Public License v3.0

using BepInEx;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ConfigurationManager.Utilities
{
    internal static class Utils
    {
        public static string ToProperCase(this string str)
        {
            if (string.IsNullOrEmpty(str)) return string.Empty;
            if (str.Length < 2) return str;

            // Start with the first character.
            string result = str.Substring(0, 1).ToUpper();

            // Add the remaining characters.
            for (int i = 1; i < str.Length; i++)
            {
                if (char.IsUpper(str[i])) result += " ";
                result += str[i];
            }

            return result;
        }

        /// <summary>
        ///     Return items with browsable attribute same as expectedBrowsable, and optionally items with no browsable attribute
        /// </summary>
        public static IEnumerable<T> FilterBrowsable<T>(this IEnumerable<T> props, bool expectedBrowsable,
            bool includeNotSet = false) where T : MemberInfo
        {
            if (includeNotSet)
                return props.Where(p => p.GetCustomAttributes(typeof(BrowsableAttribute), false).Cast<BrowsableAttribute>().All(x => x.Browsable == expectedBrowsable));

            return props.Where(p => p.GetCustomAttributes(typeof(BrowsableAttribute), false).Cast<BrowsableAttribute>().Any(x => x.Browsable == expectedBrowsable));
        }

        public static bool IsSubclassOfRawGeneric(this Type toCheck, Type generic)
        {
            while (toCheck != null && toCheck != typeof(object))
            {
                var cur = toCheck.IsGenericType ? toCheck.GetGenericTypeDefinition() : toCheck;
                if (generic == cur)
                    return true;
                toCheck = toCheck.BaseType;
            }
            return false;
        }

        // Search for objects instead of using chainloader API to find dynamically loaded plugins
        public static BaseUnityPlugin[] FindPlugins()
        {
            List<BaseUnityPlugin> plugins = new List<BaseUnityPlugin>();
            foreach(var plugin in BepInEx.Bootstrap.Chainloader.PluginInfos)
            {
                plugins.Add(plugin.Value.Instance);
            }

            return plugins.ToArray();
        }

        public static bool IsNumber(this object value) => value is sbyte
                   || value is byte
                   || value is short
                   || value is ushort
                   || value is int
                   || value is uint
                   || value is long
                   || value is ulong
                   || value is float
                   || value is double
                   || value is decimal;

        public static string AppendZero(this string s)
        {
            return !s.Contains(".") && !s.Contains(",") ? s + ".0" : s;
        }

        public static bool IsFloat(Type type)
        {
            return type == typeof(float) || type == typeof(double) || type == typeof(decimal);
        }

        public static string AppendZeroIfFloat(this string s, Type type)
        {
            return IsFloat(type) ? s.AppendZero() : s;
        }

        public static void FillTexture(this Texture2D tex, Color color)
        {
            for (var x = 0; x < tex.width; x++)
                for (var y = 0; y < tex.height; y++)
                    tex.SetPixel(x, y, color);

            tex.Apply(false);
        }
    }
}
