using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace CsvHelper.DocsGenerator.Infos
{
	public class TypeInfo : Info
	{
		private static readonly LinkGenerator linkGenerator = new LinkGenerator();

		public Type Type { get; protected set; }

		public List<Type> Attributes { get; protected set; }

		public List<ConstructorInfo> Constructors { get; protected set; }

		public List<Type> Interfaces { get; protected set; }

		public List<FieldInfo> Fields { get; protected set; }

		public List<PropertyInfo> Properties { get; protected set; }

		public List<MethodInfo> Methods { get; protected set; }

		public Stack<Type> Inheritance { get; protected set; }

		public List<Type> Implementers { get; protected set; }

		public TypeInfo(Type type, XElement xmlDocs)
		{
			Type = type;

			Interfaces = type.GetInterfaces().ToList();

			Constructors = type
				.GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
				.Select(c => new ConstructorInfo(c, xmlDocs))
				.Where(c => !(c.Parameters.Count == 0 && string.IsNullOrEmpty(c.Constructor.GetSummary())))
				.OrderBy(c => c.Parameters.Count)
				.ToList();

			Attributes = type
				.GetCustomAttributes()
				.Select(a => a.GetType())
				.OrderBy(t => t.Name)
				.ToList();

			var fieldsFlags = type.IsEnum
				? BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly
				: BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
			Fields = type
				.GetFields(fieldsFlags)
				.OrderBy(f => f.Name)
				.ToList();

			Properties = type
				.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
				.Select(p => new PropertyInfo(this, p, xmlDocs))
				.OrderBy(p => p.Property.Name)
				.ToList();

			Methods = type
				.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
				.Where(m => !m.IsSpecialName)
				.Select(m => new MethodInfo(m, xmlDocs))
				.OrderBy(m => m.Method.Name)
				.ToList();

			Inheritance = new Stack<Type>();
			if (type.BaseType != null)
			{
				Inheritance.Push(type);
				var currentType = type.BaseType;
				do
				{
					Inheritance.Push(currentType);
					currentType = currentType.BaseType;
				}
				while (currentType != null);
			}

			Implementers = new List<Type>();
			if (type.BaseType != null && type.BaseType != typeof(object))
			{
				Implementers.Add(type.BaseType);
			}

			Implementers.AddRange(Interfaces.Select(i => i));
		}
	}
}
