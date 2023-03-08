using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using System;

namespace RestWithASPNETUdemy {
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Property, AllowMultiple = true)]
    public sealed class UnisysAdditionalMetadataAttribute : Attribute {

        public UnisysAdditionalMetadataAttribute(string name, object value) {
            if (name == null) {
                throw new ArgumentNullException("name");
            }
            Name = name;
            Value = value;
        }

        public string Name { get; private set; }
        public object Value { get; private set; }
    }

    public class UnisysAdditionalMetadataProvider : IDisplayMetadataProvider {

        public UnisysAdditionalMetadataProvider() { }

        public void CreateDisplayMetadata(DisplayMetadataProviderContext context) {
            // Extract all AdditionalMetadataAttribute values and add to AdditionalValues
            if (context.PropertyAttributes != null) {
                foreach (object propAttr in context.PropertyAttributes) {
                    UnisysAdditionalMetadataAttribute addMetaAttr = propAttr as UnisysAdditionalMetadataAttribute;
                    if (addMetaAttr != null) {
                        context.DisplayMetadata.AdditionalValues.Add(addMetaAttr.Name, addMetaAttr.Value);
                    }
                }
            }
        }
    }
}
