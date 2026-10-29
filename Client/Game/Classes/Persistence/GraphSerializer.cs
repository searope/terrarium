//------------------------------------------------------------------------------
//      Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text.Json;

namespace Terrarium.Persistence
{
    /// <summary>
    ///  Saves an object graph as JSON the way BinaryFormatter did: every
    ///  instance field of a [Serializable] type except [NonSerialized] ones,
    ///  with shared references and cycles kept.  Unlike BinaryFormatter,
    ///  reading only ever creates types from a fixed list: the [Serializable]
    ///  types of the assemblies passed in, a few system types, and objects
    ///  rebuilt by the registered ISaveRecord factories.
    /// </summary>
    /// <remarks>
    ///  A value is written as a JSON primitive when its type is the field's
    ///  type, and as { "$type": ..., "$value": ... } otherwise.  Objects are
    ///  written as { "$id": n, "$type": ..., fields... } the first time and
    ///  { "$ref": n } after that.  Arrays use "$values" (and "$lengths" when
    ///  they have more than one dimension), Hashtables "$entries" and
    ///  ISaveRecord objects "$record".
    /// </remarks>
    internal sealed class GraphSerializer
    {
        private static readonly Type[] SystemTypes =
            {
                typeof (bool), typeof (byte), typeof (sbyte), typeof (char), typeof (short), typeof (ushort),
                typeof (int), typeof (uint), typeof (long), typeof (ulong), typeof (float), typeof (double),
                typeof (decimal), typeof (string), typeof (Guid), typeof (DateTime), typeof (TimeSpan),
                typeof (Point), typeof (Size), typeof (Rectangle), typeof (KnownColor),
                typeof (Hashtable), typeof (ArrayList), typeof (object)
            };

        private readonly Dictionary<string, Type> _typesByName = new Dictionary<string, Type>();
        private readonly Dictionary<Type, string> _namesByType = new Dictionary<Type, string>();
        private readonly Dictionary<Type, Func<IDictionary<string, object>, object>> _recordFactories =
            new Dictionary<Type, Func<IDictionary<string, object>, object>>();
        private readonly Dictionary<Type, List<KeyValuePair<string, FieldInfo>>> _fields =
            new Dictionary<Type, List<KeyValuePair<string, FieldInfo>>>();

        /// <summary>
        ///  Creates a serializer that can save and load the [Serializable]
        ///  types of the given assemblies.
        /// </summary>
        public GraphSerializer(params Assembly[] assemblies)
        {
            // System types keep the same name on every runtime, wherever they live
            foreach (var type in SystemTypes)
            {
                _typesByName[type.FullName] = type;
                _namesByType[type] = type.FullName;
            }

            foreach (var assembly in assemblies)
            {
                foreach (var type in assembly.GetTypes())
                {
                    if ((IsSerializableType(type) || typeof (ISaveRecord).IsAssignableFrom(type)) &&
                        !type.IsGenericTypeDefinition &&
                        !typeof (Delegate).IsAssignableFrom(type) &&
                        !typeof (Exception).IsAssignableFrom(type))
                    {
                        AddType(type);
                    }
                }
            }
        }

        /// <summary>
        ///  Registers how to rebuild an ISaveRecord type from its saved values.
        /// </summary>
        public void AddRecordFactory(Type type, Func<IDictionary<string, object>, object> factory)
        {
            AddType(type);
            _recordFactories[type] = factory;
        }

        private void AddType(Type type)
        {
            var name = type.FullName + ", " + type.Assembly.GetName().Name;
            _typesByName[name] = type;
            _namesByType[type] = name;
        }

        #region Writing

        /// <summary>
        ///  Writes a value whose declared type is given.
        /// </summary>
        public void Write(Utf8JsonWriter writer, object value, Type declaredType)
        {
            new GraphWriter(this, writer).WriteValue(value, declaredType);
        }

        private sealed class GraphWriter
        {
            private readonly GraphSerializer _owner;
            private readonly Utf8JsonWriter _writer;
            private readonly Dictionary<object, int> _ids = new Dictionary<object, int>(ReferenceComparer.Instance);

            public GraphWriter(GraphSerializer owner, Utf8JsonWriter writer)
            {
                _owner = owner;
                _writer = writer;
            }

            public void WriteValue(object value, Type declaredType)
            {
                if (value == null)
                {
                    _writer.WriteNullValue();
                    return;
                }

                var type = value.GetType();
                var exact = type == declaredType;

                // Strings and booleans in untyped slots (Hashtable keys, mostly) read
                // back unambiguously, so they don't need a type
                if (declaredType == typeof (object) && (type == typeof (string) || type == typeof (bool)))
                {
                    WriteSimple(value, type);
                    return;
                }

                if (typeof (Delegate).IsAssignableFrom(type))
                {
                    throw new InvalidOperationException("Can't save a delegate of type " + type);
                }

                if (IsSimple(type))
                {
                    if (exact)
                    {
                        WriteSimple(value, type);
                    }
                    else
                    {
                        _writer.WriteStartObject();
                        _writer.WriteString("$type", _owner.NameOf(type));
                        _writer.WritePropertyName("$value");
                        WriteSimple(value, type);
                        _writer.WriteEndObject();
                    }
                    return;
                }

                if (!type.IsValueType)
                {
                    int id;
                    if (_ids.TryGetValue(value, out id))
                    {
                        _writer.WriteStartObject();
                        _writer.WriteNumber("$ref", id);
                        _writer.WriteEndObject();
                        return;
                    }

                    id = _ids.Count + 1;
                    _ids.Add(value, id);
                    _writer.WriteStartObject();
                    _writer.WriteNumber("$id", id);
                }
                else
                {
                    _writer.WriteStartObject();
                }

                if (!exact)
                {
                    _writer.WriteString("$type", _owner.NameOf(type));
                }

                if (type.IsArray)
                {
                    WriteArray((Array) value, type.GetElementType());
                }
                else if (type == typeof (Hashtable))
                {
                    _writer.WriteStartArray("$entries");
                    foreach (DictionaryEntry entry in (Hashtable) value)
                    {
                        _writer.WriteStartArray();
                        WriteValue(entry.Key, typeof (object));
                        WriteValue(entry.Value, typeof (object));
                        _writer.WriteEndArray();
                    }
                    _writer.WriteEndArray();
                }
                else if (type == typeof (ArrayList))
                {
                    _writer.WriteStartArray("$values");
                    foreach (var item in (ArrayList) value)
                    {
                        WriteValue(item, typeof (object));
                    }
                    _writer.WriteEndArray();
                }
                else if (value is ISaveRecord)
                {
                    var values = new Dictionary<string, object>();
                    ((ISaveRecord) value).GetSaveValues(values);
                    _writer.WriteStartObject("$record");
                    foreach (var pair in values)
                    {
                        _writer.WritePropertyName(pair.Key);
                        WriteValue(pair.Value, typeof (object));
                    }
                    _writer.WriteEndObject();
                }
                else
                {
                    if (!IsSerializableType(type))
                    {
                        throw new InvalidOperationException("Can't save an object of type " + type +
                                                            "; it isn't [Serializable].");
                    }

                    foreach (var field in _owner.FieldsOf(type))
                    {
                        _writer.WritePropertyName(field.Key);
                        WriteValue(field.Value.GetValue(value), field.Value.FieldType);
                    }
                }

                _writer.WriteEndObject();
            }

            private void WriteArray(Array array, Type elementType)
            {
                if (array.Rank > 1)
                {
                    _writer.WriteStartArray("$lengths");
                    for (var dimension = 0; dimension < array.Rank; dimension++)
                    {
                        _writer.WriteNumberValue(array.GetLength(dimension));
                    }
                    _writer.WriteEndArray();
                }

                // Mostly empty arrays (the world's cell grid) are written as
                // [index, value] pairs for the elements that aren't empty
                var defaultValue = elementType.IsValueType ? Activator.CreateInstance(elementType) : null;
                var used = 0;
                foreach (var item in array)
                {
                    if (!Equals(item, defaultValue))
                    {
                        used++;
                    }
                }

                if (used * 2 < array.Length)
                {
                    if (array.Rank == 1)
                    {
                        _writer.WriteNumber("$length", array.Length);
                    }

                    _writer.WriteStartArray("$sparse");
                    var index = 0;
                    foreach (var item in array)
                    {
                        if (!Equals(item, defaultValue))
                        {
                            _writer.WriteStartArray();
                            _writer.WriteNumberValue(index);
                            WriteValue(item, elementType);
                            _writer.WriteEndArray();
                        }
                        index++;
                    }
                    _writer.WriteEndArray();
                    return;
                }

                // Elements in row-major order, as foreach enumerates them
                _writer.WriteStartArray("$values");
                foreach (var item in array)
                {
                    WriteValue(item, elementType);
                }
                _writer.WriteEndArray();
            }

            private void WriteSimple(object value, Type type)
            {
                if (type.IsEnum)
                {
                    _writer.WriteNumberValue(Convert.ToInt64(value, CultureInfo.InvariantCulture));
                    return;
                }

                switch (Type.GetTypeCode(type))
                {
                    case TypeCode.Boolean:
                        _writer.WriteBooleanValue((bool) value);
                        return;
                    case TypeCode.String:
                        _writer.WriteStringValue((string) value);
                        return;
                    case TypeCode.Char:
                        _writer.WriteStringValue(value.ToString());
                        return;
                    case TypeCode.Single:
                    case TypeCode.Double:
                        var number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                        if (double.IsNaN(number) || double.IsInfinity(number))
                        {
                            _writer.WriteStringValue(number.ToString("R", CultureInfo.InvariantCulture));
                        }
                        else if (type == typeof (float))
                        {
                            _writer.WriteNumberValue((float) value);
                        }
                        else
                        {
                            _writer.WriteNumberValue(number);
                        }
                        return;
                    case TypeCode.Decimal:
                        _writer.WriteNumberValue((decimal) value);
                        return;
                    case TypeCode.UInt64:
                        _writer.WriteNumberValue((ulong) value);
                        return;
                    case TypeCode.DateTime:
                        _writer.WriteStringValue(((DateTime) value).ToString("o", CultureInfo.InvariantCulture));
                        return;
                }

                if (type.IsPrimitive)
                {
                    _writer.WriteNumberValue(Convert.ToInt64(value, CultureInfo.InvariantCulture));
                }
                else if (type == typeof (byte[]))
                {
                    _writer.WriteBase64StringValue((byte[]) value);
                }
                else if (type == typeof (Guid))
                {
                    _writer.WriteStringValue(((Guid) value).ToString("D"));
                }
                else if (type == typeof (TimeSpan))
                {
                    _writer.WriteNumberValue(((TimeSpan) value).Ticks);
                }
                else if (type == typeof (Point))
                {
                    var point = (Point) value;
                    WriteNumbers(point.X, point.Y);
                }
                else if (type == typeof (Size))
                {
                    var size = (Size) value;
                    WriteNumbers(size.Width, size.Height);
                }
                else if (type == typeof (Rectangle))
                {
                    var rectangle = (Rectangle) value;
                    WriteNumbers(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
                }
                else
                {
                    throw new InvalidOperationException("Can't save a value of type " + type);
                }
            }

            private void WriteNumbers(params int[] numbers)
            {
                _writer.WriteStartArray();
                foreach (var number in numbers)
                {
                    _writer.WriteNumberValue(number);
                }
                _writer.WriteEndArray();
            }
        }

        #endregion

        #region Reading

        /// <summary>
        ///  Reads a value written with the same declared type.
        /// </summary>
        public object Read(JsonElement element, Type declaredType)
        {
            var reader = new GraphReader(this);
            var value = reader.ReadValue(element, declaredType);
            reader.CompleteDeserialization();
            return value;
        }

        private sealed class GraphReader
        {
            private readonly GraphSerializer _owner;
            private readonly Dictionary<int, object> _objects = new Dictionary<int, object>();
            private readonly List<IDeserializationCallback> _callbacks = new List<IDeserializationCallback>();

            public GraphReader(GraphSerializer owner)
            {
                _owner = owner;
            }

            public void CompleteDeserialization()
            {
                foreach (var callback in _callbacks)
                {
                    callback.OnDeserialization(null);
                }
            }

            public object ReadValue(JsonElement element, Type declaredType)
            {
                if (element.ValueKind == JsonValueKind.Null)
                {
                    return null;
                }

                if (declaredType == typeof (object))
                {
                    switch (element.ValueKind)
                    {
                        case JsonValueKind.String:
                            return element.GetString();
                        case JsonValueKind.True:
                            return true;
                        case JsonValueKind.False:
                            return false;
                    }
                }

                var type = declaredType;
                JsonElement typeElement;
                var isObject = element.ValueKind == JsonValueKind.Object;
                if (isObject)
                {
                    JsonElement reference;
                    if (element.TryGetProperty("$ref", out reference))
                    {
                        object target;
                        if (!_objects.TryGetValue(reference.GetInt32(), out target))
                        {
                            throw new InvalidDataException("The save file refers to an object it doesn't contain.");
                        }
                        return target;
                    }

                    if (element.TryGetProperty("$type", out typeElement))
                    {
                        type = _owner.TypeOf(typeElement.GetString());
                        if (!declaredType.IsAssignableFrom(type))
                        {
                            throw new InvalidDataException("The save file has a " + type + " where a " +
                                                           declaredType + " belongs.");
                        }
                    }
                }

                if (IsSimple(type))
                {
                    JsonElement simple;
                    if (isObject && type != declaredType && element.TryGetProperty("$value", out simple))
                    {
                        return ReadSimple(simple, type);
                    }
                    return ReadSimple(element, type);
                }

                if (!isObject)
                {
                    throw new InvalidDataException("The save file has a value where a " + type + " belongs.");
                }

                if (type.IsArray)
                {
                    return ReadArray(element, type);
                }

                if (type == typeof (Hashtable))
                {
                    var table = new Hashtable();
                    Register(element, table);
                    foreach (var entry in element.GetProperty("$entries").EnumerateArray())
                    {
                        var key = ReadValue(entry[0], typeof (object));
                        table[key] = ReadValue(entry[1], typeof (object));
                    }
                    return table;
                }

                if (type == typeof (ArrayList))
                {
                    var list = new ArrayList();
                    Register(element, list);
                    foreach (var item in element.GetProperty("$values").EnumerateArray())
                    {
                        list.Add(ReadValue(item, typeof (object)));
                    }
                    return list;
                }

                Func<IDictionary<string, object>, object> factory;
                if (_owner._recordFactories.TryGetValue(type, out factory))
                {
                    var values = new Dictionary<string, object>();
                    foreach (var property in element.GetProperty("$record").EnumerateObject())
                    {
                        values[property.Name] = ReadValue(property.Value, typeof (object));
                    }
                    var record = factory(values);
                    Register(element, record);
                    return record;
                }

                if (typeof (ISaveRecord).IsAssignableFrom(type) || type.IsAbstract || type.IsInterface ||
                    type == typeof (object))
                {
                    throw new InvalidDataException("The save file has an object of type " + type +
                                                   " that can't be created.");
                }

                var instance = CreateUninitialized(type);
                if (!type.IsValueType)
                {
                    Register(element, instance);
                }

                foreach (var field in _owner.FieldsOf(type))
                {
                    JsonElement fieldElement;
                    if (element.TryGetProperty(field.Key, out fieldElement))
                    {
                        // SetValue works on the boxed copy of a struct as well
                        field.Value.SetValue(instance, ReadValue(fieldElement, field.Value.FieldType));
                    }
                }

                var callback = instance as IDeserializationCallback;
                if (callback != null)
                {
                    _callbacks.Add(callback);
                }

                return instance;
            }

            private object ReadArray(JsonElement element, Type type)
            {
                var elementType = type.GetElementType();
                var rank = type.GetArrayRank();
                JsonElement values;
                var sparse = element.TryGetProperty("$sparse", out values);
                if (!sparse)
                {
                    values = element.GetProperty("$values");
                }

                int[] lengths;
                JsonElement lengthsElement;
                if (rank > 1)
                {
                    lengthsElement = element.GetProperty("$lengths");
                    lengths = new int[rank];
                    var dimension = 0;
                    foreach (var length in lengthsElement.EnumerateArray())
                    {
                        lengths[dimension++] = length.GetInt32();
                    }
                }
                else
                {
                    lengths = new[] {sparse ? element.GetProperty("$length").GetInt32() : values.GetArrayLength()};
                }

                var array = Array.CreateInstance(elementType, lengths);
                Register(element, array);

                if (sparse)
                {
                    foreach (var pair in values.EnumerateArray())
                    {
                        array.SetValue(ReadValue(pair[1], elementType), ToIndices(pair[0].GetInt32(), lengths));
                    }
                    return array;
                }

                var indices = new int[rank];
                foreach (var item in values.EnumerateArray())
                {
                    array.SetValue(ReadValue(item, elementType), indices);

                    // Advance the index in row-major order
                    for (var dimension = rank - 1; dimension >= 0; dimension--)
                    {
                        if (++indices[dimension] < lengths[dimension])
                        {
                            break;
                        }
                        indices[dimension] = 0;
                    }
                }

                return array;
            }

            private static int[] ToIndices(int flatIndex, int[] lengths)
            {
                if (flatIndex < 0)
                {
                    throw new InvalidDataException("The save file has an array element out of range.");
                }

                // Row-major: the last dimension changes fastest
                var indices = new int[lengths.Length];
                for (var dimension = lengths.Length - 1; dimension >= 0; dimension--)
                {
                    indices[dimension] = flatIndex % lengths[dimension];
                    flatIndex /= lengths[dimension];
                }
                if (flatIndex != 0)
                {
                    throw new InvalidDataException("The save file has an array element out of range.");
                }
                return indices;
            }

            private void Register(JsonElement element, object instance)
            {
                JsonElement id;
                if (element.TryGetProperty("$id", out id))
                {
                    _objects[id.GetInt32()] = instance;
                }
            }

            private static object ReadSimple(JsonElement element, Type type)
            {
                if (type.IsEnum)
                {
                    return Enum.ToObject(type, element.GetInt64());
                }

                switch (Type.GetTypeCode(type))
                {
                    case TypeCode.Boolean:
                        return element.GetBoolean();
                    case TypeCode.String:
                        return element.GetString();
                    case TypeCode.Char:
                        return element.GetString()[0];
                    case TypeCode.Single:
                        return (float) ReadDouble(element);
                    case TypeCode.Double:
                        return ReadDouble(element);
                    case TypeCode.Decimal:
                        return element.GetDecimal();
                    case TypeCode.UInt64:
                        return element.GetUInt64();
                    case TypeCode.DateTime:
                        return DateTime.Parse(element.GetString(), CultureInfo.InvariantCulture,
                                              DateTimeStyles.RoundtripKind);
                }

                if (type.IsPrimitive)
                {
                    return Convert.ChangeType(element.GetInt64(), type, CultureInfo.InvariantCulture);
                }
                if (type == typeof (byte[]))
                {
                    return element.GetBytesFromBase64();
                }
                if (type == typeof (Guid))
                {
                    return Guid.Parse(element.GetString());
                }
                if (type == typeof (TimeSpan))
                {
                    return new TimeSpan(element.GetInt64());
                }
                if (type == typeof (Point))
                {
                    return new Point(element[0].GetInt32(), element[1].GetInt32());
                }
                if (type == typeof (Size))
                {
                    return new Size(element[0].GetInt32(), element[1].GetInt32());
                }
                if (type == typeof (Rectangle))
                {
                    return new Rectangle(element[0].GetInt32(), element[1].GetInt32(), element[2].GetInt32(),
                                         element[3].GetInt32());
                }
                throw new InvalidDataException("Can't read a value of type " + type);
            }

            private static double ReadDouble(JsonElement element)
            {
                return element.ValueKind == JsonValueKind.String
                           ? double.Parse(element.GetString(), CultureInfo.InvariantCulture)
                           : element.GetDouble();
            }

            private static object CreateUninitialized(Type type)
            {
#if NET5_0_OR_GREATER
                return RuntimeHelpers.GetUninitializedObject(type);
#else
                return FormatterServices.GetUninitializedObject(type);
#endif
            }
        }

        #endregion

        #region Types and fields

        // [Serializable] and [NonSerialized] only mark which types and fields a
        // save may hold; nothing here uses formatter-based serialization
#pragma warning disable SYSLIB0050
        private static bool IsSerializableType(Type type)
        {
            // Enums are always serializable
            return type.IsEnum || (type.Attributes & TypeAttributes.Serializable) != 0;
        }

        private static bool IsSaved(FieldInfo field)
        {
            return (field.Attributes & FieldAttributes.NotSerialized) == 0;
        }
#pragma warning restore SYSLIB0050

        private static bool IsSimple(Type type)
        {
            return type.IsPrimitive || type.IsEnum || type == typeof (string) || type == typeof (decimal) ||
                   type == typeof (DateTime) || type == typeof (TimeSpan) || type == typeof (Guid) ||
                   type == typeof (byte[]) || type == typeof (Point) || type == typeof (Size) ||
                   type == typeof (Rectangle);
        }

        private string NameOf(Type type)
        {
            if (type.IsArray)
            {
                var rank = type.GetArrayRank();
                return NameOf(type.GetElementType()) + "[" + new string(',', rank - 1) + "]";
            }

            string name;
            if (!_namesByType.TryGetValue(type, out name))
            {
                throw new InvalidOperationException("Can't save an object of type " + type +
                                                    "; it isn't one of the types a save file may hold.");
            }
            return name;
        }

        private Type TypeOf(string name)
        {
            if (name.EndsWith("]", StringComparison.Ordinal))
            {
                var open = name.LastIndexOf('[');
                var element = TypeOf(name.Substring(0, open));
                var rank = name.Length - open - 1;
                return rank == 1 ? element.MakeArrayType() : element.MakeArrayType(rank);
            }

            Type type;
            if (!_typesByName.TryGetValue(name, out type))
            {
                throw new InvalidDataException("The save file has an object of type '" + name +
                                               "', which isn't one of the types a save file may hold.");
            }
            return type;
        }

        private List<KeyValuePair<string, FieldInfo>> FieldsOf(Type type)
        {
            List<KeyValuePair<string, FieldInfo>> fields;
            if (_fields.TryGetValue(type, out fields))
            {
                return fields;
            }

            var all = new List<FieldInfo>();
            for (var current = type; current != null && current != typeof (object) && current != typeof (ValueType);
                 current = current.BaseType)
            {
                foreach (var field in current.GetFields(BindingFlags.Instance | BindingFlags.Public |
                                                        BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (IsSaved(field))
                    {
                        all.Add(field);
                    }
                }
            }

            // Base classes can have private fields with the same name as a
            // derived class's; qualify those with the declaring type
            var counts = new Dictionary<string, int>();
            foreach (var field in all)
            {
                int count;
                counts.TryGetValue(field.Name, out count);
                counts[field.Name] = count + 1;
            }

            fields = new List<KeyValuePair<string, FieldInfo>>();
            foreach (var field in all)
            {
                var key = counts[field.Name] > 1 ? field.DeclaringType.Name + "." + field.Name : field.Name;
                fields.Add(new KeyValuePair<string, FieldInfo>(key, field));
            }

            _fields[type] = fields;
            return fields;
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();

            public new bool Equals(object x, object y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }

        #endregion
    }
}
