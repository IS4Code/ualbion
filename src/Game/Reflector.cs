﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;

namespace UAlbion.Game
{
    public static class Reflector
    {
        public class ReflectedObject
        {
            public ReflectedObject(ReflectedObject parent, int index) { Parent = parent; CollectionIndex = index; }
            public ReflectedObject Parent { get; }
            public int CollectionIndex { get; }
            public object Object { get; set; }
            public string Name { get; set; }
            public string Value { get; set; }
            public IEnumerable<ReflectedObject> SubObjects { get; set; }
        }

        static object GetPropertySafe(PropertyInfo x, object o)
        {
            try
            {
                return !x.CanRead ? "<< No Getter! >>" : x.GetValue(o);
            }
            catch (Exception e) { return e; }
        }

        static object GetFieldSafe(FieldInfo x, object o)
        {
            try { return x.GetValue(o); }
            catch (Exception e) { return e; }
        }

        public static ReflectedObject Reflect(string name, object o, ReflectedObject parent, int collectionIndex = 0)
        {
            var result = ReflectCommonType(name, o, parent, collectionIndex);
            if (result != null)
                return result;

            result = new ReflectedObject(parent, collectionIndex);
            var t = o.GetType();
            result.Name = name;
            result.Value = o.ToString();
            result.Object = o;
            var publicProperties =
                t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => !x.GetIndexParameters().Any());
            var publicFields = t.GetFields(BindingFlags.Public | BindingFlags.Instance);
            var privateProperties =
                t.GetProperties(BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(x => !x.GetIndexParameters().Any());
            var privateFields = t.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(x => !x.Name.Contains("__BackingField"));

            ReflectedObject FormatProperty(PropertyInfo x) => Reflect(x.Name, GetPropertySafe(x, o), result);
            ReflectedObject FormatField(FieldInfo x) => Reflect(x.Name, GetFieldSafe(x, o), result);

            result.SubObjects =
                publicProperties.Select(FormatProperty)
                .Concat(publicFields.Select(FormatField)).OrderBy(x => x.Name)
                .Concat(
                    privateProperties.Select(FormatProperty)
                    .Concat(privateFields.Select(FormatField)).OrderBy(x => x.Name));

            return result;
        }

        public static ReflectedObject ReflectCommonType(string name, object o, ReflectedObject parent, int index)
        {
            switch (o)
            {
                case null:
                    return new ReflectedObject(parent, index)
                    { Name = name, Object = null, Value = "null" };
                case string s:
                    return new ReflectedObject(parent, index)
                    { Name = name, Object = o, Value = $"\"{s.Replace("\"", "\\\"")}\"" };

                case bool _:   case byte _:
                case ushort _: case short _:
                case uint _:   case int _:
                case ulong _:  case long _:
                case float _:  case double _:
                    return new ReflectedObject(parent, index)
                    { Name = name, Object = o, Value = o.ToString() };
                case Enum e:
                    return new ReflectedObject(parent, index)
                    { Name = name, Object = o, Value = e.ToString() };
                case Vector2 v:
                    return new ReflectedObject(parent, index)
                    { Name = name, Object = o, Value = $"({v.X}, {v.Y})".ToString() };
                case Vector3 v:
                    return new ReflectedObject(parent, index)
                    { Name = name, Object = o, Value = $"({v.X}, {v.Y}, {v.Z})".ToString() };
                case ICollection e:
                    var coll = new ReflectedObject(parent, index) { Name = name, Object = o, Value = e.Count.ToString() };
                    coll.SubObjects = e.Cast<object>().Select((x, i) => Reflect(i.ToString(), x, coll, i));
                    return coll;

                case IEnumerable e:
                    var enumerable = new ReflectedObject(parent, index) { Name = name, Object = o, Value = "", };
                    enumerable.SubObjects = e.Cast<object>().Select((x, i) => Reflect(i.ToString(), x, enumerable, i));
                    return enumerable;
            }

            return null;
        }
    }
}
