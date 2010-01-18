using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using ServiceStack.Common.Extensions;

namespace ServiceStack.Common.Text
{
	public static class ToStringMethods
	{
		public delegate string ToStringDelegate(object value);

		public static string ToString(object value)
		{
			var toStringMethod = GetToStringMethod(value.GetType());
			return toStringMethod(value);
		}

		public static Func<object, string> GetToStringMethod<T>()
		{
			var type = typeof(T);

			return GetToStringMethod(type);
		}

		private static readonly Dictionary<Type, Func<object, string>> ToStringMethodCache 
			= new Dictionary<Type, Func<object, string>>();

		public static Func<object, string> GetToStringMethod(Type type)
		{
			Func<object, string> toStringMethod;
			lock (ToStringMethodCache)
			{
				if (!ToStringMethodCache.TryGetValue(type, out toStringMethod))
				{
					toStringMethod = GetToStringMethodToCache(type);
					ToStringMethodCache[type] = toStringMethod;
				}
			}

			return toStringMethod;
		}

		private static Func<object, string> GetToStringMethodToCache(Type type)
		{
			if (type == typeof(string))
			{
				return x => StringToString((string)x);
			}

			if (type.IsValueType)
			{
				if (type == typeof(DateTime))
					return value => DateTimeToString((DateTime)value);

				if (type == typeof(DateTime?))
					return value => value == null ? null : DateTimeToString((DateTime)value);

				return BuiltinToString;
			}

			if (type.IsArray)
			{
				if (type == typeof(byte[]))
				{
					return x => BytesToString((byte[])x);
				}
				if (type == typeof(string[]))
				{
					return x => StringArrayToString((string[])x);
				}

				return GetArrayToStringMethod(type.GetElementType());
			}

			var isCollection = type.FindInterfaces((x, y) => x == typeof(ICollection), null).Length > 0;
			if (isCollection)
			{
				var isDictionary = type.IsAssignableFrom(typeof(IDictionary))
					|| type.FindInterfaces((x, y) => x == typeof(IDictionary), null).Length > 0;

				if (isDictionary)
				{
					return obj => IDictionaryToString((IDictionary)obj);
				}

				return obj => IEnumerableToString((IEnumerable)obj);
			}

			var isEnumerable = type.IsAssignableFrom(typeof(IEnumerable))
				|| type.FindInterfaces((x, y) => x == typeof(IEnumerable), null).Length > 0;

			if (isEnumerable)
			{
				return obj => IEnumerableToString((IEnumerable)obj);
			}

			if (type.IsClass)
			{
				var typeToStringMethod = TypeToStringMethods.GetToStringMethod(type);
				if (typeToStringMethod != null)
				{
					return typeToStringMethod;
				}
			}

			return BuiltinToString;
		}

		static Dictionary<DateTime, string> dateTimeValues = new Dictionary<DateTime, string>();

		/// <summary>
		/// DateTime.ToString() is really slow, need to cache values or return ticks.
		/// </summary>
		/// <param name="dateTime">The date time.</param>
		/// <returns></returns>
		public static string DateTimeToString(DateTime dateTime)
		{
			string dateTimeString;
			lock (dateTimeValues)
			{
				if (!dateTimeValues.TryGetValue(dateTime, out dateTimeString))
				{
					if (dateTimeValues.Count > 100)
					{
						dateTimeValues = new Dictionary<DateTime, string>();
					}
					dateTimeString = dateTime.ToString();
					dateTimeValues[dateTime] = dateTimeString;
				}
			}
			return dateTimeString;
		}

		public static string StringArrayToString(string[] arrayValue)
		{
			var sb = new StringBuilder();
			var arrayValueLength = arrayValue.Length;
			for (var i=0; i < arrayValueLength; i++)
			{
				if (sb.Length > 0) sb.Append(TextExtensions.ItemSeperator);
				sb.Append(arrayValue[i].ToSafeString());
			}
			return sb.ToString();
		}

		public static Func<object, string> GetArrayToStringMethod(Type elementType)
		{
			var mi = typeof(ToStringMethods).GetMethod("ArrayToString",
				BindingFlags.Static | BindingFlags.Public);

			var genericMi = mi.MakeGenericMethod(new[] { elementType });
			var genericDelegate = (ToStringDelegate)Delegate.CreateDelegate(typeof(ToStringDelegate), genericMi);

			return genericDelegate.Invoke;
		}

		public static string ArrayToString<T>(object oArrayValue)
		{
			Func<object, string> toStringFn = null;

			var arrayValue = (T[])oArrayValue;
			var sb = new StringBuilder();
			var arrayValueLength = arrayValue.Length;
			for (var i=0; i < arrayValueLength; i++)
			{
				var item = arrayValue[i];
				if (toStringFn == null)
				{
					toStringFn = GetToStringMethod(item.GetType());
				}

				var itemString = toStringFn(item);
				if (sb.Length > 0)
				{
					sb.Append(TextExtensions.ItemSeperator);
				}
				sb.Append(itemString);
			}
			return sb.ToString();
		}

		public static string StringToString(string value)
		{
			return value.ToSafeString();
		}

		public static string BuiltinToString(object value)
		{
			return value == null ? null : value.ToString();
		}

		public static string BytesToString(byte[] byteValue)
		{
			return byteValue == null ? null : Encoding.Default.GetString(byteValue);
		}

		public static string IListGenericToString(IList list)
		{
			Func<object, string> toStringFn = null;

			var sb = new StringBuilder();
			var listLength = list.Count;
			for (var i=0; i < listLength; i++)
			{
				var item = list[i];
				if (toStringFn == null)
				{
					toStringFn = GetToStringMethod(item.GetType());
				}

				var itemString = toStringFn(item);
				if (sb.Length > 0)
				{
					sb.Append(TextExtensions.ItemSeperator);
				}
				sb.Append(itemString);
			}

			return sb.ToString();
		}

		public static string IEnumerableToString(IEnumerable valueCollection)
		{
			Func<object,string> toStringFn = null;

			var sb = new StringBuilder();
			foreach (var valueItem in valueCollection)
			{
				if (toStringFn == null)
				{
					toStringFn = GetToStringMethod(valueItem.GetType());
				}

				var elementValueString = toStringFn(valueItem);
				if (sb.Length > 0)
				{
					sb.Append(TextExtensions.ItemSeperator);
				}
				sb.Append(elementValueString);
			}
			return sb.ToString();
		}

		public static string IDictionaryToString(IDictionary valueDictionary)
		{
			Func<object,string> toStringKeyFn = null;
			Func<object,string> toStringValueFn = null;

			var sb = new StringBuilder();
			foreach (var key in valueDictionary.Keys)
			{
				var dictionaryValue = valueDictionary[key];
				if (toStringKeyFn == null)
				{
					toStringKeyFn = GetToStringMethodToCache(key.GetType());
				}
				if (toStringValueFn == null)
				{
					toStringValueFn = GetToStringMethodToCache(dictionaryValue.GetType());
				}
				var keyString = toStringKeyFn(key);
				var valueString = dictionaryValue != null ? toStringValueFn(dictionaryValue) : string.Empty;

				if (sb.Length > 0)
				{
					sb.Append(TextExtensions.ItemSeperator);
				}
				sb.Append(keyString)
					.Append(TextExtensions.KeyValueSeperator)
					.Append(valueString);
			}
			return sb.ToString();
		}

	}
}