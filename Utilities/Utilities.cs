﻿// Based on code made by MarC0 / ManlyMarco
// Copyright 2018 GNU General Public License v3.0

using BepInEx;
using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using UnityEngine;
using System.Globalization;

namespace ConfigurationManager.Utilities
{
    public static class Utils
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
            return SettingSearcher.FindPlugins();
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
            return !s.Contains(".") ? s + ".0" : s;
        }

        public static bool TryParseFloat(string input, out float result)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                result = 0;
                return false;
            }

            return float.TryParse(input.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }

        public static bool IsFloat(Type type)
        {
            return type == typeof(float) || type == typeof(double) || type == typeof(decimal);
        }

        public static string AppendZeroIfFloat(this string s, Type type)
        {
            return IsFloat(type) ? s.Replace(',', '.').AppendZero() : s;
        }

        public static float RoundWithPrecision(float value, int precision)
        {
            int multiplier = Convert.ToInt32(Math.Pow(10, precision));
            return Mathf.Round(value * multiplier) / multiplier;
        }

        public static byte RoundColor(float color) => (byte)Mathf.Round(color * 255);

        public static Color RoundColorToHEX(Color color)
        {
            byte r = RoundColor(color.r);
            byte g = RoundColor(color.g);
            byte b = RoundColor(color.b);
            byte a = RoundColor(color.a);

            return new Color32(r, g, b, a);
        }

        public static void FillTexture(this Texture2D tex, Color color)
        {
            if (color.a < 1f)
            {
                // SetPixel ignores alpha, so we need to lerp manually
                for (var x = 0; x < tex.width; x++)
                {
                    for (var y = 0; y < tex.height; y++)
                    {
                        var origColor = tex.GetPixel(x, y);
                        var lerpedColor = Color.Lerp(origColor, color, color.a);
                        // Not accurate, but good enough for our purposes
                        lerpedColor.a = Mathf.Max(origColor.a, color.a);
                        tex.SetPixel(x, y, lerpedColor);
                    }
                }
            }
            else
            {
                for (var x = 0; x < tex.width; x++)
                    for (var y = 0; y < tex.height; y++)
                        tex.SetPixel(x, y, color);
            }

            tex.Apply(false);
        }

        public static void OpenLog()
        {

        }

        public static void OpenWebsite(string url)
        {

        }

        public static string GetWebsite(BaseUnityPlugin bepInPlugin)
        {
            if (bepInPlugin == null) return null;
            try
            {
                var fileName = bepInPlugin.Info.Location; //.GetType().Assembly.Location;
                if (!File.Exists(fileName)) return null;
                var fi = FileVersionInfo.GetVersionInfo(fileName);
                return new[]
                {
                    fi.CompanyName,
                    fi.FileDescription,
                    fi.Comments,
                    fi.LegalCopyright,
                    fi.LegalTrademarks
                }.FirstOrDefault(x => Uri.IsWellFormedUriString(x, UriKind.Absolute));
            }
            catch (Exception e)
            {
                ConfigurationManager.LogWarning($"Failed to get URI for {bepInPlugin.Info?.Metadata?.Name} - {e.Message}");
                return null;
            }
        }

        public static GUIStyle CreateCopy(this GUIStyle original)
        {
            return new GUIStyle(original);
        }

        #region Resizing

        private static bool _handleClicked;
        private static Vector3 _clickedPosition;
        private static Rect _originalWindow;
        private static int _currentWindowId;
        private static bool _handleVertical;
        private static bool _handleHorizontal;

        const int functionalAreaSize = 20;
        const int verticalAreaSize = 10;
        const int horizontalAreaSize = 10;

        public static Rect ResizeWindow(int id, Rect rect, out bool sizeChanged)
        {
            sizeChanged = false;

            float visibleAreaSize = 10f;
            GUI.Box(new Rect(rect.width - visibleAreaSize - 2, rect.height - visibleAreaSize - 2, visibleAreaSize, visibleAreaSize), GUIContent.none, ConfigurationManagerStyles.GetButtonStyle());

            if (_currentWindowId != 0 && _currentWindowId != id)
                return rect;

            Vector3 mousePos = GUI.matrix.inverse.MultiplyPoint(UnityInput.Current.mousePosition);

            mousePos.x = Mathf.Clamp(mousePos.x, 0, ConfigurationManager.instance.ScreenWidth);
            mousePos.y = Mathf.Clamp(mousePos.y, 0, ConfigurationManager.instance.ScreenHeight);

            mousePos.y = ConfigurationManager.instance.ScreenHeight - mousePos.y; // Convert to GUI coords

            bool handleBoth = new Rect(
                rect.x + rect.width - functionalAreaSize,
                rect.y + rect.height - functionalAreaSize,
                functionalAreaSize,
                functionalAreaSize).Contains(mousePos);

            bool handleVertical = handleBoth || new Rect(
                rect.x + rect.width - verticalAreaSize,
                rect.y + verticalAreaSize,
                verticalAreaSize,
                rect.height - verticalAreaSize).Contains(mousePos);

            bool handleHorizontal = handleBoth || new Rect(
                rect.x,
                rect.y + rect.height - horizontalAreaSize,
                rect.width,
                horizontalAreaSize).Contains(mousePos);

            if (UnityInput.Current.GetMouseButtonDown(0) && (handleVertical || handleHorizontal))
            {
                _handleClicked = true;
                _clickedPosition = mousePos;
                _originalWindow = rect;
                _currentWindowId = id;
                _handleVertical = handleVertical;
                _handleHorizontal = handleHorizontal;
            }

            if (_handleClicked)
            {
                // Resize window by dragging. Size will be clamped later.
                rect.size = new Vector2(_handleVertical ? _originalWindow.width + (Math.Min(mousePos.x, ConfigurationManager.instance.ScreenWidth - 5f) - _clickedPosition.x) : rect.width,
                                        _handleHorizontal ? _originalWindow.height + (Math.Min(mousePos.y, ConfigurationManager.instance.ScreenHeight - 5f) - _clickedPosition.y) : rect.height);
                
                sizeChanged = true;
            }

            if (UnityInput.Current.GetMouseButtonUp(0))
            {
                _handleClicked = false;
                _currentWindowId = 0;
            }

            return rect;
        }

        #endregion
    }
}
