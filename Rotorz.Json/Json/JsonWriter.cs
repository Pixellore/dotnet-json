﻿// Copyright (c) Rotorz Limited. All rights reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Rotorz.Json {

	/// <summary>
	/// Writes JSON encoded string and accepts several formatting settings. This class is
	/// particularly useful when manually writing JSON content.
	/// </summary>
	/// <remarks>
	/// <para>Each <see cref="JsonNode"/> has as custom implementation of <see cref="JsonNode.ToString()"/>
	/// and <see cref="JsonNode.ToString(JsonWriterSettings)"/> which produce a JSON encoded strings:</para>
	/// <code language="csharp"><![CDATA[
	/// var card = new JsonObjectNode();
	/// card["Name"] = "Jessica";
	/// card["Age"] = 24;
	/// string json = card.ToString();
	/// ]]></code>
	/// <para>Alternative the more verbose implementation would be the following:</para>
	/// <code language="csharp"><![CDATA[
	/// var writer = JsonWriter.Create();
	/// card.WriteTo(writer);
	/// var json = writer.ToString();
	/// ]]></code>
	/// </remarks>
	public sealed class JsonWriter : IJsonWriter {

		#region Factory Methods

		/// <summary>
		/// Create new <see cref="JsonWriter"/> instance with custom settings.
		/// </summary>
		/// <param name="settings">Custom settings.</param>
		/// <returns>
		/// New <see cref="JsonWriter"/> instance.
		/// </returns>
		public static JsonWriter Create(JsonWriterSettings settings) {
			return new JsonWriter(null, settings);
		}

		/// <summary>
		/// Create new <see cref="JsonWriter"/> instance.
		/// </summary>
		/// <returns>
		/// New <see cref="JsonWriter"/> instance.
		/// </returns>
		public static JsonWriter Create() {
			return new JsonWriter(null, null);
		}

		/// <summary>
		/// Create new <see cref="JsonWriter"/> instance and write content to the
		/// provided <see cref="StringBuilder"/> instance with custom settings.
		/// </summary>
		/// <param name="builder">String builder.</param>
		/// <param name="settings">Custom settings.</param>
		/// <returns>
		/// New <see cref="JsonWriter"/> instance.
		/// </returns>
		public static JsonWriter Create(StringBuilder builder, JsonWriterSettings settings) {
			return new JsonWriter(builder, settings);
		}

		/// <summary>
		/// Create new <see cref="JsonWriter"/> instance and write content to the
		/// provided <see cref="StringBuilder"/> instance.
		/// </summary>
		/// <param name="builder">String builder.</param>
		/// <returns>
		/// New <see cref="JsonWriter"/> instance.
		/// </returns>
		public static JsonWriter Create(StringBuilder builder) {
			return Create(builder, null);
		}

		#endregion

		private StringBuilder _builder;

		/// <summary>
		/// Initializes a new instance of the <see cref="JsonWriter"/> class.
		/// </summary>
		/// <param name="builder">String builder.</param>
		/// <param name="settings">Custom settings; specify a value of <c>null</c> to
		/// assume default settings.</param>
		private JsonWriter(StringBuilder builder, JsonWriterSettings settings) {
			if (builder == null)
				builder = new StringBuilder();
			if (settings == null)
				settings = JsonWriterSettings.DefaultSettings;

			_builder = builder;
			Settings = settings;

			settings.IsReadOnly = true;

			_contextStack.Push(Context.Root);
		}

		#region Low Level Writing

		private enum Context {
			Root,
			Object,
			Array,
		}

		private class ContextStack {

			private List<Context> _stack = new List<Context>();

			public void Push(Context context) {
				_stack.Add(context);
			}

			public Context Pop() {
				var top = Peek();
				_stack.RemoveAt(_stack.Count - 1);
				return top;
			}

			public Context Peek() {
				if (_stack.Count == 0)
					throw new InvalidOperationException("Cannot access next context when stack is empty.");
				return _stack[_stack.Count - 1];
			}

			public int Count {
				get { return _stack.Count; }
			}

		}

		private ContextStack _contextStack = new ContextStack();
		private bool _empty = true;

		/// <summary>
		/// Gets writer settings which are used to control formatting of output. Setting
		/// properties become read-only once assigned to a <see cref="JsonWriter"/>
		/// instance.
		/// </summary>
		public JsonWriterSettings Settings { get; private set; }

		private void WriteIndent() {
			if (Settings.Indent == true) {
				int count = _contextStack.Count;
				while (--count > 0)
					_builder.Append(Settings.IndentChars);
			}
		}

		private void WriteLine() {
			if (Settings.Indent == true)
				_builder.Append(Settings.NewLineChars);
		}

		private void WriteSpace() {
			if (Settings.Indent == true)
				_builder.Append(" ");
		}

		private void WriteEscapedLiteral(string value) {
			if (value == null)
				return;

			for (int i = 0; i < value.Length; ++i) {
				char c = value[i];
				switch (c) {
					case '\"':
						_builder.Append("\\\"");
						break;
					case '\\':
						_builder.Append("\\\\");
						break;
					case '/':
						_builder.Append("\\/");
						break;
					case '\b':
						_builder.Append("\\b");
						break;
					case '\f':
						_builder.Append("\\f");
						break;
					case '\n':
						_builder.Append("\\n");
						break;
					case '\r':
						_builder.Append("\\r");
						break;
					case '\t':
						_builder.Append("\\t");
						break;
					default:
						_builder.Append(c);
						break;
				}
			}
		}

		private void DoBeginValue() {
			if (!_empty)
				_builder.Append(',');

			if (_contextStack.Peek() == Context.Array) {
				WriteLine();
				WriteIndent();
			}
		}

		private void DoEndValue() {
			_empty = false;
		}

		/// <summary>
		/// Write start of object '{'.
		/// </summary>
		/// <example>
		/// <para>This method is useful when outputting object notation:</para>
		/// <code language="csharp"><![CDATA[
		/// writer.WriteStartObject();
		/// writer.WritePropertyKey("Name");
		/// writer.WriteValue("Bob");
		/// writer.WriteEndObject();
		/// ]]></code>
		/// <para>Which generates the following JSON nodes:</para>
		/// <code><![CDATA[
		/// {
		///     "Name": "Bob"
		/// }
		/// ]]></code>
		/// </example>
		/// <seealso cref="WritePropertyKey(string)"/>
		/// <seealso cref="WriteEndObject()"/>
		public void WriteStartObject() {
			DoBeginValue();

			_builder.Append('{');

			_contextStack.Push(Context.Object);
			_empty = true;
		}

		/// <summary>
		/// Write end of object '}'.
		/// </summary>
		/// <seealso cref="WriteStartObject()"/>
		/// <seealso cref="WritePropertyKey(string)"/>
		public void WriteEndObject() {
			_contextStack.Pop();

			if (!_empty) {
				WriteLine();
				WriteIndent();
			}

			_builder.Append('}');

			DoEndValue();
		}

		/// <summary>
		/// Write property key; special characters are automatically escaped.
		/// </summary>
		/// <example>
		/// <para>This method is useful when outputting object notation:</para>
		/// <code language="csharp"><![CDATA[
		/// writer.WriteStartObject();
		/// writer.WritePropertyKey("Name");
		/// writer.WriteValue("Bob");
		/// writer.WriteEndObject();
		/// ]]></code>
		/// <para>Which generates the following JSON nodes:</para>
		/// <code><![CDATA[
		/// {
		///     "Name": "Bob"
		/// }
		/// ]]></code>
		/// </example>
		/// <param name="key">Key value.</param>
		/// <seealso cref="WriteStartObject()"/>
		/// <seealso cref="WritePropertyKey(string)"/>
		public void WritePropertyKey(string key) {
			DoBeginValue();

			WriteLine();
			WriteIndent();

			_builder.Append("\"");
			WriteEscapedLiteral(key);
			_builder.Append("\":");

			WriteSpace();

			_empty = true;
		}

		/// <summary>
		/// Write raw JSON value.
		/// </summary>
		/// <remarks>
		/// <para>Whitespace is still automatically added when specified; for instance,
		/// value will be indented if <see cref="JsonWriterSettings.Indent"/> is set to a
		/// value of <c>true</c>.</para>
		/// <para>If <paramref name="content"/> is a value of <c>null</c> then the value
		/// "null" is written to output.</para>
		/// </remarks>
		/// <param name="content">String to be written verbatim.</param>
		private void WriteValueRaw(string content) {
			DoBeginValue();

			_builder.Append(content ?? "null");

			DoEndValue();
		}

		/// <summary>
		/// Write start of array marker '['.
		/// </summary>
		/// <example>
		/// <para>This method is useful when outputting arrays:</para>
		/// <code language="csharp"><![CDATA[
		/// writer.WriteStartArray();
		/// writer.WriteValue("Bob");
		/// writer.WriteValue("Jessica");
		/// writer.WriteValue("Sandra");
		/// writer.WriteEndArray();
		/// ]]></code>
		/// <para>Which generates the following JSON nodes:</para>
		/// <code><![CDATA[
		/// [
		///     "Bob",
		///     "Jessica",
		///     "Sandra"
		/// ]
		/// ]]></code>
		/// </example>
		/// <seealso cref="WriteEndArray"/>
		public void WriteStartArray() {
			DoBeginValue();

			_builder.Append('[');

			_contextStack.Push(Context.Array);
			_empty = true;
		}

		/// <summary>
		/// Write end of array marker ']'.
		/// </summary>
		/// <seealso cref="WriteStartArray"/>
		public void WriteEndArray() {
			_contextStack.Pop();
			
			if (!_empty) {
				WriteLine();
				WriteIndent();
			}

			_builder.Append(']');

			DoEndValue();
		}

		#endregion
		
		/// <inheritdoc/>
		public void WriteObject(IDictionary<string, JsonNode> collection) {
			if (collection == null)
				throw new ArgumentNullException("collection");

			WriteStartObject();

			foreach (var property in collection) {
				WritePropertyKey(property.Key);

				if (property.Value != null)
					property.Value.WriteTo(this);
				else
					WriteNull();
			}

			WriteEndObject();
		}

		/// <inheritdoc/>
		public void WriteArray(IList<JsonNode> collection) {
			if (collection == null)
				throw new ArgumentNullException("collection");

			WriteStartArray();

			foreach (var node in collection)
				if (node != null)
					node.WriteTo(this);
				else
					WriteNull();

			WriteEndArray();
		}

		/// <inheritdoc/>
		public void WriteNull() {
			WriteValueRaw("null");
		}

		/// <inheritdoc/>
		public void WriteInteger(long value) {
			WriteValueRaw(value.ToString(CultureInfo.InvariantCulture));
		}

		/// <inheritdoc/>
		public void WriteDouble(double value) {
			WriteValueRaw(JsonFormattingUtility.DoubleToString(value));
		}

		/// <inheritdoc/>
		public void WriteString(string value) {
			DoBeginValue();

			_builder.Append('"');
			WriteEscapedLiteral(value);
			_builder.Append('"');

			DoEndValue();
		}

		/// <inheritdoc/>
		public void WriteBoolean(bool value) {
			WriteValueRaw(value ? "true" : "false");
		}

		/// <inheritdoc/>
		public void WriteBinary(byte[] value) {
			if (value == null)
				throw new ArgumentNullException("value");

			WriteStartArray();
			for (int i = 0; i < value.Length; ++i)
				WriteInteger(value[i]);
			WriteEndArray();
		}

		/// <summary>
		/// Get current state of JSON encoded string which is being written.
		/// </summary>
		/// <returns>
		/// JSON encoded string.
		/// </returns>
		public override string ToString() {
			return _builder.ToString();
		}

	}

}
