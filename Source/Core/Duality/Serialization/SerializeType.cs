﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Duality.Serialization
{
	/// <summary>
	/// The SerializeType class is essentially caching serialization-relevant information
	/// that has been generated basing on a <see cref="System.Type"/>.
	/// </summary>
	[DontSerialize]
	public sealed class SerializeType
	{
		private TypeInfo    type;
		private FieldInfo[] fields;
		private string      typeString;
		private DataType    dataType;
		private bool        dontSerialize;
		private object      defaultValue;

		/// <summary>
		/// [GET] The <see cref="System.Reflection.TypeInfo"/> that is described.
		/// </summary>
		public TypeInfo Type
		{
			get { return this.type; }
		}
		/// <summary>
		/// [GET] An array of <see cref="System.Reflection.FieldInfo">fields</see> which are serialized.
		/// </summary>
		public FieldInfo[] Fields
		{
			get { return this.fields; }
		}
		/// <summary>
		/// [GET] A string referring to the <see cref="System.Type"/> that is described.
		/// </summary>
		/// <seealso cref="ReflectionHelper.GetTypeId"/>
		public string TypeString
		{
			get { return this.typeString; }
		}
		/// <summary>
		/// [GET] The <see cref="Duality.Serialization.DataType"/> associated with the described <see cref="System.Type"/>.
		/// </summary>
		public DataType DataType
		{
			get { return this.dataType; }
		}
		/// <summary>
		/// [GET] Returns whether objects of this Type are viable for serialization. 
		/// </summary>
		public bool IsSerializable
		{
			get { return !this.dontSerialize; }
		}
		/// <summary>
		/// [GET] Returns whether object of this Type can be referenced by other serialized objects.
		/// </summary>
		public bool CanBeReferenced
		{
			get
			{
				return !this.type.IsValueType && (
					this.dataType == DataType.Array || 
					this.dataType == DataType.Struct || 
					this.dataType == DataType.Delegate || 
					this.dataType.IsMemberInfoType());
			}
		}
		/// <summary>
		/// [GET] Returns the default instance for objects of this type. This is a cached instance
		/// of <see cref="ObjectCreator.GetDefaultOf"/>.
		/// </summary>
		public object DefaultValue
		{
			get { return this.defaultValue; }
		}

		/// <summary>
		/// Creates a new SerializeType based on a <see cref="System.Type"/>, gathering all the information that is necessary for serialization.
		/// </summary>
		/// <param name="t"></param>
		public SerializeType(Type t)
		{
			this.type = t.GetTypeInfo();
			this.typeString = t.GetTypeId();
			this.dataType = GetDataType(this.type);
			this.dontSerialize = this.type.HasAttributeCached<DontSerializeAttribute>();
			this.defaultValue = this.type.GetDefaultOf();

			if (this.dataType == DataType.Struct)
			{
				// Retrieve all fields that are not flagged not to be serialized
				IEnumerable<FieldInfo> filteredFields = this.type
					.DeclaredFieldsDeep()
					.Where(f => !f.IsStatic && !f.HasAttributeCached<DontSerializeAttribute>());

				// Ugly hack to skip .Net collection _syncRoot fields. 
				// Can't use field.IsNonSerialized, because that doesn't exist in the PCL profile,
				// and implementing a whole filtering system just for this would be overkill.
				filteredFields = filteredFields
					.Where(f => !(
						f.FieldType == typeof(object) && 
						f.Name == "_syncRoot" && 
						typeof(System.Collections.ICollection).GetTypeInfo().IsAssignableFrom(f.DeclaringType.GetTypeInfo())));

				// Store the filtered fields in a fixed form
				this.fields = filteredFields.ToArray();
				this.fields.StableSort((a, b) => string.Compare(a.Name, b.Name));
			}
			else
			{
				this.fields = new FieldInfo[0];
			}
		}

		private static DataType GetDataType(TypeInfo typeInfo)
		{
			Type type = typeInfo.AsType();
			if (typeInfo.IsEnum)
				return DataType.Enum;
			else if (typeInfo.IsPrimitive)
			{
				if		(type == typeof(bool))		return DataType.Bool;
				else if (type == typeof(byte))		return DataType.Byte;
				else if (type == typeof(char))		return DataType.Char;
				else if (type == typeof(sbyte))		return DataType.SByte;
				else if (type == typeof(short))		return DataType.Short;
				else if (type == typeof(ushort))	return DataType.UShort;
				else if (type == typeof(int))		return DataType.Int;
				else if (type == typeof(uint))		return DataType.UInt;
				else if (type == typeof(long))		return DataType.Long;
				else if (type == typeof(ulong))		return DataType.ULong;
				else if (type == typeof(float))		return DataType.Float;
				else if (type == typeof(double))	return DataType.Double;
				else if (type == typeof(decimal))	return DataType.Decimal;
			}
			else if (typeof(Type).GetTypeInfo().IsAssignableFrom(typeInfo))
				return DataType.Type;
			else if (typeof(MemberInfo).GetTypeInfo().IsAssignableFrom(typeInfo))
				return DataType.MemberInfo;
			else if (typeof(Delegate).GetTypeInfo().IsAssignableFrom(typeInfo))
				return DataType.Delegate;
			else if (type == typeof(string))
				return DataType.String;
			else if (typeInfo.IsArray)
				return DataType.Array;
			else if (typeInfo.IsClass)
				return DataType.Struct;
			else if (typeInfo.IsValueType)
				return DataType.Struct;

			// Should never happen in theory
			return DataType.Unknown;
		}
	}
}
