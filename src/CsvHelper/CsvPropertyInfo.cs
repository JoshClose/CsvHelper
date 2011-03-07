using System;
using System.ComponentModel;
using System.Reflection;

namespace CsvHelper
{
    public class CsvPropertyInfo
    {
        private readonly PropertyInfo property;
        private readonly CsvFieldAttribute csvAttribute;


        public CsvPropertyInfo(PropertyInfo property)
        {
            if (property == null)
            {
                throw new ArgumentNullException("property");
            }

            this.property = property;
            this.csvAttribute = ReflectionHelper.GetAttribute<CsvFieldAttribute>(property, false);
        }

        public bool Ignore
        {
            get
            {
                if (csvAttribute != null)
                {
                    return csvAttribute.Ignore;
                }
                return false;
            }
        }

        public bool HasFieldIndex
        {
            get
            {
                if (csvAttribute != null)
                {
                    return csvAttribute.FieldIndex >= 0;
                }
                return false;
            }            
        }

        public string Name
        {
            get
            {
                if (csvAttribute != null
                    && !String.IsNullOrEmpty(csvAttribute.FieldName))
                {
                    return csvAttribute.FieldName;
                }
                return property.Name;
            }            
        }

        public int FieldIndex
        {
            get
            {
                if (csvAttribute != null)                    
                {
                    return csvAttribute.FieldIndex;
                }
                return -1;
            }
        }

        public PropertyInfo Property
        {
            get { return property; }
        }

        public bool HasAttribute
        {
            get { return csvAttribute != null; }
        }

        public TypeConverter FindTypeConverter()
        {
            TypeConverter typeConverter = null;
            var typeConverterAttribute = ReflectionHelper.GetAttribute<TypeConverterAttribute>(property, false);
            if (typeConverterAttribute != null)
            {
                var typeConverterType = Type.GetType(typeConverterAttribute.ConverterTypeName);
                if (typeConverterType != null)
                {
                    typeConverter = Activator.CreateInstance(typeConverterType) as TypeConverter;
                }
            }
            return typeConverter;
        }
    }
}
